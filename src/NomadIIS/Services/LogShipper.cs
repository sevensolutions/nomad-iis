using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NomadIIS.Services;

/// <summary>
/// Tails the log files matching a pattern and ships the appended bytes into the named pipe
/// which nomad's logmon exposes as the task's stdout. This gives us <c>nomad alloc logs</c>
/// support for IIS tasks, which otherwise have no console attached to their w3wp processes.
/// </summary>
internal sealed class LogShipper : IAsyncDisposable
{
	/// <summary>
	/// How long we wait for logmon's pipe before giving up and retrying on the next poll.
	/// </summary>
	private const int ConnectTimeoutMs = 5000;

	/// <summary>
	/// If a file produces this many bytes without a single newline we give up on waiting for
	/// a line boundary and ship what we have, so that a log without trailing newlines cannot
	/// stall the shipper forever.
	/// </summary>
	private const int MaxPendingBytes = 256 * 1024;

	private readonly ILogger _logger;
	private readonly string _taskId;
	private readonly string _stdoutPath;
	private readonly string _rootDirectory;
	private readonly string _pattern;
	private readonly TimeSpan _pollInterval;
	private readonly long _maxBytesPerPoll;

	/// <summary>
	/// When true the files which already exist on the first poll are seeded to their current
	/// length instead of being read from the start. Used when recovering a task, because
	/// logmon has already persisted everything written before the driver restarted and
	/// re-shipping it would duplicate those lines.
	/// </summary>
	private readonly bool _startAtEnd;

	private readonly Dictionary<string, TailedFile> _files = new( StringComparer.OrdinalIgnoreCase );
	private bool _primed;

	private NamedPipeClientStream? _pipe;

	public LogShipper (
		ILogger logger,
		string taskId,
		string stdoutPath,
		string rootDirectory,
		string pattern,
		TimeSpan pollInterval,
		long maxBytesPerSecond,
		bool startAtEnd )
	{
		_logger = logger;
		_taskId = taskId;
		_stdoutPath = stdoutPath;
		_rootDirectory = rootDirectory;
		_pattern = pattern;
		_pollInterval = pollInterval < TimeSpan.FromMilliseconds( 100 )
			? TimeSpan.FromMilliseconds( 100 )
			: pollInterval;
		_startAtEnd = startAtEnd;

		// Turn the byte/second budget into a per-poll budget so a chatty application cannot
		// flood the logmon pipe.
		_maxBytesPerPoll = Math.Max( 4096L, (long)( maxBytesPerSecond * _pollInterval.TotalSeconds ) );
	}

	public async Task RunAsync ( CancellationToken cancellationToken )
	{
		_logger.LogInformation( $"Task {_taskId}: Shipping logs from {Path.Combine( _rootDirectory, _pattern )}." );

		while ( !cancellationToken.IsCancellationRequested )
		{
			try
			{
				await PumpAsync( cancellationToken );
			}
			catch ( OperationCanceledException )
			{
				break;
			}
			catch ( Exception ex )
			{
				// Never let a transient IO or pipe error kill the shipper. The most likely cause
				// is logmon restarting underneath us, which the next poll recovers from.
				_logger.LogWarning( ex, $"Task {_taskId}: Failed to ship logs. Retrying in {_pollInterval}." );

				await ResetPipeAsync();
			}

			try
			{
				await Task.Delay( _pollInterval, cancellationToken );
			}
			catch ( OperationCanceledException )
			{
				break;
			}
		}

		_logger.LogInformation( $"Task {_taskId}: Stopped shipping logs." );
	}

	private async Task PumpAsync ( CancellationToken cancellationToken )
	{
		var matches = ResolveFiles();

		// Forget files which have been deleted or rotated out of the pattern, so the offset
		// dictionary cannot grow without bound over the lifetime of a long-running task.
		foreach ( var gone in _files.Keys.Where( x => !matches.Contains( x, StringComparer.OrdinalIgnoreCase ) ).ToArray() )
			_files.Remove( gone );

		foreach ( var path in matches )
		{
			if ( !_files.TryGetValue( path, out var file ) )
			{
				file = new TailedFile();

				// Files that already existed when a recovered task started tailing are skipped
				// up to their current length. Anything appearing later is new output and is
				// shipped in full.
				if ( !_primed && _startAtEnd )
					file.Offset = TryGetLength( path );

				_files.Add( path, file );
			}

			await PumpFileAsync( path, file, cancellationToken );
		}

		_primed = true;
	}

	private async Task PumpFileAsync ( string path, TailedFile file, CancellationToken cancellationToken )
	{
		byte[] chunk;

		try
		{
			// FileShare.ReadWrite | FileShare.Delete is essential: logging frameworks such as
			// Serilog and NLog keep the file open for writing and rename/delete it when rolling.
			using var fs = new FileStream(
				path, FileMode.Open, FileAccess.Read,
				FileShare.ReadWrite | FileShare.Delete,
				bufferSize: 4096, useAsync: true );

			if ( fs.Length < file.Offset )
			{
				// The file shrank, so it was truncated or replaced in place. Start over.
				file.Offset = 0;
				file.Pending = Array.Empty<byte>();
			}

			if ( fs.Length <= file.Offset )
				return;

			var toRead = (int)Math.Min( fs.Length - file.Offset, _maxBytesPerPoll );

			fs.Seek( file.Offset, SeekOrigin.Begin );

			var buffer = new byte[toRead];

			var read = await fs.ReadAtLeastAsync( buffer, toRead, throwOnEndOfStream: false, cancellationToken );

			file.Offset += read;

			chunk = read == toRead ? buffer : buffer[..read];
		}
		catch ( FileNotFoundException )
		{
			// Rotated away between globbing and opening.
			return;
		}
		catch ( DirectoryNotFoundException )
		{
			return;
		}

		if ( chunk.Length == 0 )
			return;

		var complete = TakeCompleteLines( file, chunk );

		if ( complete.Length > 0 )
			await WriteToPipeAsync( complete, cancellationToken );
	}

	/// <summary>
	/// Appends <paramref name="chunk"/> to the file's pending buffer and returns only the part
	/// ending at the last newline. Shipping whole lines keeps the output readable in the nomad
	/// UI and, because we read at byte offsets, also avoids splitting a multi-byte UTF-8
	/// sequence across two writes.
	/// </summary>
	internal static byte[] TakeCompleteLines ( TailedFile file, byte[] chunk )
	{
		var combined = file.Pending.Length == 0 ? chunk : [.. file.Pending, .. chunk];

		var cut = Array.LastIndexOf( combined, (byte)'\n' );

		if ( cut < 0 )
		{
			if ( combined.Length >= MaxPendingBytes )
			{
				file.Pending = Array.Empty<byte>();
				return combined;
			}

			file.Pending = combined;
			return Array.Empty<byte>();
		}

		file.Pending = combined[( cut + 1 )..];

		return combined[..( cut + 1 )];
	}

	private async Task WriteToPipeAsync ( byte[] data, CancellationToken cancellationToken )
	{
		if ( _pipe is null || !_pipe.IsConnected )
		{
			await ResetPipeAsync();

			_pipe = new NamedPipeClientStream(
				".", GetPipeName( _stdoutPath ), PipeDirection.Out, PipeOptions.Asynchronous );

			await _pipe.ConnectAsync( ConnectTimeoutMs, cancellationToken );
		}

		await _pipe.WriteAsync( data, cancellationToken );
		await _pipe.FlushAsync( cancellationToken );
	}

	private async Task ResetPipeAsync ()
	{
		if ( _pipe is not null )
		{
			try
			{
				await _pipe.DisposeAsync();
			}
			catch ( Exception )
			{
				// Nothing useful we can do while tearing down a broken pipe.
			}

			_pipe = null;
		}
	}

	/// <summary>
	/// Returns the full paths matching the configured pattern, oldest first so that output is
	/// shipped in roughly chronological order when a pattern matches several files.
	/// </summary>
	private string[] ResolveFiles ()
	{
		var relativeDirectory = Path.GetDirectoryName( _pattern );
		var mask = Path.GetFileName( _pattern );

		if ( string.IsNullOrEmpty( mask ) )
			return Array.Empty<string>();

		var searchDirectory = string.IsNullOrEmpty( relativeDirectory )
			? _rootDirectory
			: Path.Combine( _rootDirectory, relativeDirectory );

		var di = new DirectoryInfo( searchDirectory );

		if ( !di.Exists )
			return Array.Empty<string>();

		return di
			.EnumerateFiles( mask, SearchOption.TopDirectoryOnly )
			.OrderBy( x => x.LastWriteTimeUtc )
			.Select( x => x.FullName )
			.ToArray();
	}

	private static long TryGetLength ( string path )
	{
		try
		{
			return new FileInfo( path ).Length;
		}
		catch ( Exception )
		{
			return 0;
		}
	}

	/// <summary>
	/// Nomad hands us the stdout path as <c>//./pipe/&lt;name&gt;</c> on Windows.
	/// </summary>
	internal static string GetPipeName ( string stdoutPath )
	{
		var name = stdoutPath.Replace( '\\', '/' );

		const string prefix = "//./pipe/";

		if ( name.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
			name = name[prefix.Length..];

		return name;
	}

	public async ValueTask DisposeAsync ()
		=> await ResetPipeAsync();

	internal sealed class TailedFile
	{
		public long Offset { get; set; }

		/// <summary>
		/// An incomplete trailing line carried over to the next poll.
		/// </summary>
		public byte[] Pending { get; set; } = Array.Empty<byte>();
	}
}
