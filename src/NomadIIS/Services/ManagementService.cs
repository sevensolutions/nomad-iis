using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Web.Administration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NomadIIS.Services;

public sealed class ManagementService
{
	private bool _driverEnabled;
	private TimeSpan _statsInterval = TimeSpan.FromSeconds( 3 );
	private TimeSpan _fingerprintInterval = TimeSpan.FromSeconds( 30 );
	private readonly ConcurrentDictionary<string, IisTaskHandle> _handles = new();
	private readonly SemaphoreSlim _lock = new( 1, 1 );
	private readonly Channel<DriverTaskEvent> _eventsChannel = Channel.CreateUnbounded<DriverTaskEvent>( new UnboundedChannelOptions()
	{
		SingleWriter = false,
		SingleReader = true,
		AllowSynchronousContinuations = false,
	} );

	public ManagementService ()
	{
	}

	public bool DriverEnabled => _driverEnabled;
	public TimeSpan StatsInterval => _statsInterval;
	public TimeSpan FingerprintInterval => _fingerprintInterval;

	public void Configure ( bool enabled, TimeSpan? statsInterval, TimeSpan? fingerprintInterval )
	{
		_driverEnabled = enabled;
		_statsInterval = statsInterval ?? _statsInterval;
		_fingerprintInterval = fingerprintInterval ?? _fingerprintInterval;
	}

	public IisTaskHandle CreateHandle ( string taskId )
		=> _handles.GetOrAdd( taskId, id => new IisTaskHandle( this, id ) );

	public IisTaskHandle GetHandle ( string taskId )
		=> TryGetHandle( taskId ) ?? throw new TaskNotFoundException( taskId );
	public IisTaskHandle? TryGetHandle ( string taskId )
	{
		if ( _handles.TryGetValue( taskId, out var handle ) )
			return handle;
		return null;
	}

	internal async Task LockAsync ( Func<ServerManager, Task> action )
	{
		await _lock.WaitAsync();

		var serverManager = new ServerManager();

		try
		{
			await action( serverManager );

			serverManager.CommitChanges();
		}
		finally
		{
			serverManager.Dispose();

			_lock.Release();
		}
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
		finally
		{
			serverManager.Dispose();

			_lock.Release();
		}
	}

	internal void Delete ( IisTaskHandle handle )
		=> _handles.TryRemove( handle.TaskId, out _ );

	public ValueTask SendEventAsync ( DriverTaskEvent @event )
		=> _eventsChannel.Writer.WriteAsync( @event );

	public IAsyncEnumerable<DriverTaskEvent> ReadAllEventsAsync ( CancellationToken cancellationToken )
		=> _eventsChannel.Reader.ReadAllAsync( cancellationToken );
}
