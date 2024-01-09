using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NomadIIS.Services;

public sealed class ManagementService
{
	private const string StateFilename = "nomad_iis.state.json";

	private readonly ILogger<ManagementService> _logger;
	private bool _driverEnabled = true;
	private string _dataDirectory;
	private TimeSpan _fingerprintInterval = TimeSpan.FromSeconds( 30 );
	private bool _directorySecurity = true;
	private string[] _allowedTargetWebsites = Array.Empty<string>();
	private readonly ConcurrentDictionary<string, IisTaskHandle> _handles = new();
	private readonly SemaphoreSlim _lock = new( 1, 1 );
	private readonly object _stateLock = new object();
	private readonly Channel<DriverTaskEvent> _eventsChannel = Channel.CreateUnbounded<DriverTaskEvent>( new UnboundedChannelOptions()
	{
		SingleWriter = false,
		SingleReader = true,
		AllowSynchronousContinuations = false,
	} );

	public ManagementService ( ILogger<ManagementService> logger )
	{
		_logger = logger;
		_dataDirectory = Path.GetDirectoryName( typeof( Program ).Assembly.Location )!;
	}

	public bool DriverEnabled => _driverEnabled;
	public TimeSpan FingerprintInterval => _fingerprintInterval;
	public bool DirectorySecurity => _directorySecurity;
	public string[] AllowedTargetWebsites => _allowedTargetWebsites;

	public void Configure ( DriverConfig config )
	{
		_driverEnabled = config.Enabled;

		if ( !string.IsNullOrEmpty( config.DataDirectory ) )
		{
			if ( Path.IsPathRooted( config.DataDirectory ) )
				_dataDirectory = config.DataDirectory;
			else
				_dataDirectory = Path.Combine( _dataDirectory, config.DataDirectory );
		}

		if ( !Directory.Exists( _dataDirectory ) )
			Directory.CreateDirectory( _dataDirectory );

		if ( config.FingerprintInterval < TimeSpan.FromSeconds( 10 ) )
			throw new ArgumentException( $"fingerprint_interval must be at least 10s." );

		_fingerprintInterval = config.FingerprintInterval;
		_directorySecurity = config.DirectorySecurity;
		_allowedTargetWebsites = config.AllowedTargetWebsites ?? Array.Empty<string>();

		LoadState();
	}

	public IisTaskHandle CreateHandle ( string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		return _handles.GetOrAdd( taskId, id => new IisTaskHandle( this, id ) );
	}

	public IisTaskHandle GetHandle ( string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		return TryGetHandle( taskId ) ?? throw new TaskNotFoundException( taskId );
	}
	public IisTaskHandle? TryGetHandle ( string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		if ( _handles.TryGetValue( taskId, out var handle ) )
			return handle;
		return null;
	}

	internal async Task LockAsync ( Func<ServerManager, Task> action )
	{
		_ = await LockAsync( async serverManager =>
		{
			await action( serverManager );
			return true;
		} );
	}
	internal async Task<T> LockAsync<T> ( Func<ServerManager, Task<T>> action )
	{
		await _lock.WaitAsync();

		var serverManager = new ServerManager();

		try
		{
			var result = await action( serverManager );

			serverManager.CommitChanges();

			return result;
		}
		catch ( UnauthorizedAccessException ex )
		{
			_logger.LogError( ex, ex.Message );

			throw;
		}
		finally
		{
			serverManager.Dispose();

			_lock.Release();
		}
	}

	internal void Delete ( IisTaskHandle handle )
		=> _handles.TryRemove( handle.TaskId, out _ );

	internal void SaveState ()
	{
		lock ( _stateLock )
		{
			var stateFile = Path.Combine( _dataDirectory, StateFilename );

			var driverState = new DriverState();

			driverState.Allocations = _handles.Values.Select( x => new DriverStateAlloc()
			{
				TaskId = x.TaskId,
				//AllocId = x.Con
			} ).ToArray();

			var stateJson = JsonSerializer.Serialize( driverState );

			File.WriteAllText( stateFile, stateJson );
		}
	}
	private void LoadState ()
	{
		lock ( _stateLock )
		{
			var stateFile = Path.Combine( _dataDirectory, StateFilename );

			if ( File.Exists( stateFile ) )
			{
				var json = File.ReadAllText( stateFile );

				var stateJson = JsonSerializer.Deserialize<DriverState>( json );
			}
		}
	}

	public ValueTask SendEventAsync ( DriverTaskEvent @event )
		=> _eventsChannel.Writer.WriteAsync( @event );

	public IAsyncEnumerable<DriverTaskEvent> ReadAllEventsAsync ( CancellationToken cancellationToken )
		=> _eventsChannel.Reader.ReadAllAsync( cancellationToken );
}
