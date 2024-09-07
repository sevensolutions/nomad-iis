﻿using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Linq;

namespace NomadIIS.Services;

public sealed class ManagementService : IHostedService
{
	private readonly ILogger<ManagementService> _logger;
	private bool _driverEnabled = true;
	private TimeSpan _fingerprintInterval = TimeSpan.FromSeconds( 30 );
	private bool _directorySecurity = true;
	private string[] _allowedTargetWebsites = Array.Empty<string>();
	private int? _udpLoggerPort;
	private UdpClient? _udpLoggerClient;
	private Task? _udpLoggerTask;
	private CancellationTokenSource _cts = new CancellationTokenSource();
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
	public string[] AllowedTargetWebsites => _allowedTargetWebsites;
	public int? UdpLoggerPort => _udpLoggerPort;

	public async void Configure ( DriverConfig config )
	{
		_driverEnabled = config.Enabled;

		if ( config.FingerprintInterval < TimeSpan.FromSeconds( 10 ) )
			throw new ArgumentException( $"fingerprint_interval must be at least 10s." );

		_fingerprintInterval = config.FingerprintInterval;
		_directorySecurity = config.DirectorySecurity;
		_allowedTargetWebsites = config.AllowedTargetWebsites ?? Array.Empty<string>();

		// Setup UDP logger endpoint
		if ( config.UdpLoggerPort is not null && config.UdpLoggerPort.Value > 0 && _udpLoggerClient is null )
		{
			_udpLoggerPort = config.UdpLoggerPort;

			var ipEndpoint = new IPEndPoint( IPAddress.Loopback, _udpLoggerPort.Value );

			_udpLoggerClient = new UdpClient( ipEndpoint );

			_udpLoggerTask = await Task.Factory.StartNew( UdpLoggerReceiverTask, TaskCreationOptions.LongRunning );
		}
	}

	public Task StartAsync ( CancellationToken cancellationToken )
		=> Task.CompletedTask;

	public async Task StopAsync ( CancellationToken cancellationToken )
	{
		_cts.Cancel();

		if ( _udpLoggerTask is not null )
			await _udpLoggerTask;
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

	private async Task UdpLoggerReceiverTask()
	{
		if ( _udpLoggerClient is null )
			return;

		while ( !_cts.IsCancellationRequested )
		{
			try
			{
				var result = await _udpLoggerClient.ReceiveAsync( _cts.Token );

				var handle = _handles.Values
					.FirstOrDefault( x => x.UdpLoggerPort == result.RemoteEndPoint.Port );

				if ( handle is not null )
					await handle.ShipLogsAsync( _logger, result.Buffer );
			}
			catch ( Exception ex )
			{
				_logger.LogWarning( ex, "Failed to process UDP logger message." );
			}
		}
	}

	public ValueTask SendEventAsync ( DriverTaskEvent @event )
		=> _eventsChannel.Writer.WriteAsync( @event );

	public IAsyncEnumerable<DriverTaskEvent> ReadAllEventsAsync ( CancellationToken cancellationToken )
		=> _eventsChannel.Reader.ReadAllAsync( cancellationToken );
}
