using Microsoft.Extensions.Logging.Abstractions;
using NomadIIS.Services;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace NomadIIS.Tests;

public class LogShipperTests
{
	[Theory]
	[InlineData( "//./pipe/foo-bar", "foo-bar" )]
	[InlineData( @"\\.\pipe\foo-bar", "foo-bar" )]
	[InlineData( "foo-bar", "foo-bar" )]
	public void GetPipeNameStripsThePrefix ( string stdoutPath, string expected )
		=> Assert.Equal( expected, LogShipper.GetPipeName( stdoutPath ) );

	[Fact]
	public void PartialLinesAreBufferedUntilTheNewlineArrives ()
	{
		var file = new LogShipper.TailedFile();

		Assert.Empty( LogShipper.TakeCompleteLines( file, Encoding.UTF8.GetBytes( "partial" ) ) );
		Assert.Equal( "partial", Encoding.UTF8.GetString( file.Pending ) );

		var complete = LogShipper.TakeCompleteLines( file, Encoding.UTF8.GetBytes( " line\nnext" ) );

		Assert.Equal( "partial line\n", Encoding.UTF8.GetString( complete ) );
		Assert.Equal( "next", Encoding.UTF8.GetString( file.Pending ) );
	}

	[Fact]
	public void MultiByteCharactersAreNeverSplit ()
	{
		var file = new LogShipper.TailedFile();
		var bytes = Encoding.UTF8.GetBytes( "grüß dich\n" );

		// Feed the file one byte per poll. Nothing may be emitted before the newline, which is
		// what guarantees we never cut a UTF-8 sequence in half.
		for ( var i = 0; i < bytes.Length - 1; i++ )
			Assert.Empty( LogShipper.TakeCompleteLines( file, [bytes[i]] ) );

		var complete = LogShipper.TakeCompleteLines( file, [bytes[^1]] );

		Assert.Equal( "grüß dich\n", Encoding.UTF8.GetString( complete ) );
		Assert.Empty( file.Pending );
	}

	[Fact]
	public async Task AppendedLinesAreShipped ()
	{
		var shipped = await ShipAsync(
			pattern: "app.log",
			produce: async root =>
			{
				await File.WriteAllTextAsync( Path.Combine( root, "app.log" ), "one\ntwo\n" );
				await Task.Delay( 300 );
				await File.AppendAllTextAsync( Path.Combine( root, "app.log" ), "three\n" );
			},
			until: "three\n" );

		Assert.Equal( "one\ntwo\nthree\n", shipped );
	}

	[Fact]
	public async Task IncompleteTrailingLinesAreHeldBack ()
	{
		var shipped = await ShipAsync(
			pattern: "app.log",
			produce: async root =>
			{
				await File.WriteAllTextAsync( Path.Combine( root, "app.log" ), "complete\nincomp" );
				await Task.Delay( 300 );
				await File.AppendAllTextAsync( Path.Combine( root, "app.log" ), "lete\n" );
			},
			until: "incomplete\n" );

		Assert.Equal( "complete\nincomplete\n", shipped );
	}

	[Fact]
	public async Task RotationToANewFileIsFollowed ()
	{
		var shipped = await ShipAsync(
			pattern: "app_*.log",
			produce: async root =>
			{
				await File.WriteAllTextAsync( Path.Combine( root, "app_1.log" ), "from-first\n" );
				await Task.Delay( 300 );
				await File.WriteAllTextAsync( Path.Combine( root, "app_2.log" ), "from-second\n" );
			},
			until: "from-second\n" );

		Assert.Contains( "from-first\n", shipped );
		Assert.Contains( "from-second\n", shipped );
	}

	[Fact]
	public async Task TruncationRestartsFromTheBeginning ()
	{
		var shipped = await ShipAsync(
			pattern: "app.log",
			produce: async root =>
			{
				var path = Path.Combine( root, "app.log" );

				await File.WriteAllTextAsync( path, "a-long-first-line\n" );
				await Task.Delay( 300 );

				// Shorter than what we already consumed, which is how we detect a truncation.
				await File.WriteAllTextAsync( path, "short\n" );
			},
			until: "short\n" );

		Assert.Equal( "a-long-first-line\nshort\n", shipped );
	}

	[Fact]
	public async Task RecoveredTasksSkipWhatLogmonAlreadyHas ()
	{
		var shipped = await ShipAsync(
			pattern: "app.log",
			startAtEnd: true,
			prepare: root => File.WriteAllTextAsync( Path.Combine( root, "app.log" ), "before-restart\n" ),
			produce: async root =>
			{
				await Task.Delay( 300 );
				await File.AppendAllTextAsync( Path.Combine( root, "app.log" ), "after-restart\n" );
			},
			until: "after-restart\n" );

		Assert.Equal( "after-restart\n", shipped );
	}

	/// <summary>
	/// Runs a <see cref="LogShipper"/> against a throwaway directory, standing in for nomad's
	/// logmon with a named pipe server, and returns everything the shipper wrote to that pipe
	/// once the output ends with <paramref name="until"/>.
	/// </summary>
	private static async Task<string> ShipAsync (
		string pattern,
		Func<string, Task> produce,
		string until,
		bool startAtEnd = false,
		Func<string, Task>? prepare = null )
	{
		var root = Path.Combine( Path.GetTempPath(), $"nomad-iis-logshipper-{Guid.NewGuid():N}" );

		Directory.CreateDirectory( root );

		var pipeName = $"nomad-iis-test-{Guid.NewGuid():N}";

		using var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 30 ) );

		try
		{
			if ( prepare is not null )
				await prepare( root );

			using var server = new NamedPipeServerStream(
				pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous );

			var connected = server.WaitForConnectionAsync( cts.Token );

			await using var shipper = new LogShipper(
				NullLogger.Instance,
				"test-task",
				$"//./pipe/{pipeName}",
				root,
				pattern,
				TimeSpan.FromMilliseconds( 100 ),
				maxBytesPerSecond: 1024 * 1024,
				startAtEnd );

			var running = shipper.RunAsync( cts.Token );

			await produce( root );
			await connected;

			var received = new StringBuilder();
			var buffer = new byte[4096];

			while ( !received.ToString().EndsWith( until, StringComparison.Ordinal ) )
			{
				var read = await server.ReadAsync( buffer, cts.Token );

				if ( read == 0 )
					break;

				received.Append( Encoding.UTF8.GetString( buffer, 0, read ) );
			}

			await cts.CancelAsync();
			await running;

			return received.ToString();
		}
		finally
		{
			try
			{
				Directory.Delete( root, true );
			}
			catch ( Exception )
			{
				// Best effort only, the temp directory is disposable.
			}
		}
	}
}
