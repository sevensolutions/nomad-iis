﻿using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Logging;
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
	private readonly ILogger<ManagementService> _logger;
	private bool _driverEnabled;
	private TimeSpan _fingerprintInterval = TimeSpan.FromSeconds( 30 );
	private bool _directorySecurity;
	private readonly ConcurrentDictionary<string, IisTaskHandle> _handles = new();
	private readonly SemaphoreSlim _lock = new( 1, 1 );
	private readonly Channel<DriverTaskEvent> _eventsChannel = Channel.CreateUnbounded<DriverTaskEvent>( new UnboundedChannelOptions()
	{
		SingleWriter = false,
		SingleReader = true,
		AllowSynchronousContinuations = false,
	} );

	public ManagementService ( ILogger<ManagementService> logger )
	{
		_logger = logger;
	}

	public bool DriverEnabled => _driverEnabled;
	public TimeSpan FingerprintInterval => _fingerprintInterval;
	public bool DirectorySecurity => _directorySecurity;

	public void Configure ( bool enabled, TimeSpan? fingerprintInterval, bool directorySecurity )
	{
		_driverEnabled = enabled;
		_fingerprintInterval = fingerprintInterval ?? _fingerprintInterval;
		_directorySecurity = directorySecurity;
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

	public ValueTask SendEventAsync ( DriverTaskEvent @event )
		=> _eventsChannel.Writer.WriteAsync( @event );

	public IAsyncEnumerable<DriverTaskEvent> ReadAllEventsAsync ( CancellationToken cancellationToken )
		=> _eventsChannel.Reader.ReadAllAsync( cancellationToken );
}
