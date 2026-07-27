using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NomadIIS.Services;

public sealed class ManagementService : IHostedService
{
	private readonly ILogger<ManagementService> _logger;
	private bool _driverEnabled = true;
	private TimeSpan _fingerprintInterval = TimeSpan.FromSeconds( 30 );
	private bool _directorySecurity = true;
	private string[] _allowedTargetWebsites = Array.Empty<string>();
	private string[] _allowedAppPoolIdentities = ["ApplicationPoolIdentity"];
	private string[] _allowedAppPoolUsers = Array.Empty<string>();
	private string? _placeholderAppPath;
	private DriverConfigProcdump? _procdumpConfig;
	private CancellationTokenSource _cts = new CancellationTokenSource();
	private readonly ConcurrentDictionary<string, IisTaskHandle> _handles = new();
	private readonly SemaphoreSlim _lock = new( 1, 1 );
	private Thread? _jobStatisticsThread;
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
	public string[] AllowedAppPoolIdentities => _allowedAppPoolIdentities;
	public string[] AllowedAppPoolUsers => _allowedAppPoolUsers;
	public string? PlaceholderAppPath => _placeholderAppPath;
	public string? ProcdumpBinaryPath => _procdumpConfig?.BinaryPath ?? "C:\\procdump.exe";
	public bool ProcdumpEulaAccepted => _procdumpConfig?.AcceptEula ?? false;

	public Task Configure ( DriverConfig config )
	{
		_driverEnabled = config.Enabled;

		if ( config.FingerprintInterval < TimeSpan.FromSeconds( 10 ) )
			throw new ArgumentException( $"fingerprint_interval must be at least 10s." );

		_fingerprintInterval = config.FingerprintInterval;
		_directorySecurity = config.DirectorySecurity;
		_allowedTargetWebsites = config.AllowedTargetWebsites ?? Array.Empty<string>();
		_allowedAppPoolIdentities = config.AllowedAppPoolIdentities ?? ["ApplicationPoolIdentity"];
		_allowedAppPoolUsers = config.AllowedAppPoolUsers ?? Array.Empty<string>();
		_placeholderAppPath = config.PlaceholderAppPath;
		_procdumpConfig = config.Procdumps.Length == 1 ? config.Procdumps[0] : null;

		return Task.CompletedTask;
	}

	public Task StartAsync ( CancellationToken cancellationToken )
	{
		_jobStatisticsThread = new Thread( JobStatisticsLoop );
		_jobStatisticsThread.Start();

		return Task.CompletedTask;
	}
	public Task StopAsync ( CancellationToken cancellationToken )
	{
		_cts.Cancel();

		return Task.CompletedTask;
	}

	public IisTaskHandle CreateHandle ( string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		return _handles.GetOrAdd( taskId, id => new IisTaskHandle( this, _logger, id ) );
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
	public IisTaskHandle? TryGetHandleByAllocIdAndTaskName ( string allocId, string taskName )
	{
		var handles = _handles.Values.ToArray();

		return handles.SingleOrDefault(
			x => x.TaskConfig != null && x.TaskConfig.AllocId == allocId && x.TaskConfig.Name == taskName );
	}

	/// <summary>
	/// Returns a current snapshot of the IIS handles without any locking.
	/// Do not modify any of these handles.
	/// </summary>
	/// <returns></returns>
	internal IisTaskHandle[] GetHandlesSnapshot ()
		=> _handles.Values.ToArray();

	internal async Task LockAsync ( Func<IManagementLockHandle, Task> action, CancellationToken cancellationToken = default )
	{
		_ = await LockAsync( async lockHandle =>
		{
			await action( lockHandle );
			return true;
		}, cancellationToken );
	}

	internal async Task<T> LockAsync<T> ( Func<IManagementLockHandle, Task<T>> action, CancellationToken cancellationToken = default )
	{
		await _lock.WaitAsync( cancellationToken );

		ManagementLockHandle? handle = null;

		try
		{
			using ( var serverManager = new ServerManager() )
			{
				handle = new ManagementLockHandle( _logger, serverManager );

				var result = await action( handle );

				serverManager.CommitChanges();

				return result;
			}
		}
		catch ( Exception ex )
		{
			_logger.LogError( ex, ex.Message );

			if ( handle is not null )
				await handle.RollbackAsync();

			throw;
		}
		finally
		{
			_lock.Release();
		}
	}

	internal void Delete ( IisTaskHandle handle )
		=> _handles.TryRemove( handle.TaskId, out _ );

	public ValueTask SendEventAsync ( DriverTaskEvent @event )
		=> _eventsChannel.Writer.WriteAsync( @event );

	public IAsyncEnumerable<DriverTaskEvent> ReadAllEventsAsync ( CancellationToken cancellationToken )
		=> _eventsChannel.Reader.ReadAllAsync( cancellationToken );

	private class ManagementLockHandle : IManagementLockHandle
	{
		private readonly ILogger _logger;
		private List<(Func<Task> Action, Func<Task> RollbackAction)>? _txActions;

		public ManagementLockHandle ( ILogger logger, ServerManager serverManager )
		{
			_logger = logger;

			ServerManager = serverManager;
		}

		public ServerManager ServerManager { get; }

		public async Task JoinTransactionAsync ( Func<Task> action, Func<Task> rollbackAction )
		{
			if ( _txActions is null )
				_txActions = new List<(Func<Task> Action, Func<Task> RollbackAction)>();

			_txActions.Add( (action, rollbackAction) );

			await action();
		}

		public async Task RollbackAsync ()
		{
			if ( _txActions is not null )
			{
				foreach ( var action in _txActions )
				{
					try
					{
						await action.RollbackAction();
					}
					catch ( Exception ex )
					{
						_logger.LogError( ex, "Failed to rollback." );
					}
				}
			}
		}
	}

	private async void JobStatisticsLoop ()
	{
		// Add some initial delay
		await Task.Delay( 5000 );

		while ( !_cts.IsCancellationRequested )
		{
			try
			{
				var jobHandles = _handles.Values.ToArray();

				// Note: We find the app pool of each worker process by parsing the -ap argument from its command line.
				// This is better than using the IIS Management API or WMI because both leak memory when queried continuously,
				// and unlike matching by process username, it also works when the pool runs under a custom user.
				var stats = new Dictionary<string, UsageStatistics>( StringComparer.InvariantCultureIgnoreCase );

				foreach ( var process in Process.GetProcessesByName( "w3wp" ) )
				{
					using ( process )
					{
						try
						{
							if ( process.HasExited )
								continue;

							var appPoolName = GetAppPoolNameFromCommandLine( process );

							if ( string.IsNullOrEmpty( appPoolName ) )
								continue;

							// TimeSpan.Ticks are in 100ns units, the same unit WMI reported the processor times in.
							var kernelModeTime = (ulong)process.PrivilegedProcessorTime.Ticks;
							var userModeTime = (ulong)process.UserProcessorTime.Ticks;
							var workingSetPrivate = NativeFunctions.GetPrivateWorkingSet( process );

							if ( stats.TryGetValue( appPoolName, out var appPoolStats ) )
							{
								stats[appPoolName] = new UsageStatistics(
									appPoolStats.KernelModeTime + kernelModeTime,
									appPoolStats.UserModeTime + userModeTime,
									appPoolStats.WorkingSetPrivate + workingSetPrivate );
							}
							else
								stats[appPoolName] = new UsageStatistics( kernelModeTime, userModeTime, workingSetPrivate );
						}
						catch ( Exception )
						{
							// The process may have exited in the meantime.
						}
					}
				}

				foreach ( var jobHandle in jobHandles )
				{
					if ( jobHandle.AppPoolNames is not null && stats.Count > 0 )
					{
						var fullStats = new UsageStatistics();

						// If we have a task with multiple application pools, we simply sum up all the resources.
						foreach ( var appPoolName in jobHandle.AppPoolNames )
						{
							if ( stats.TryGetValue( appPoolName.Value, out var jobStats ) )
							{
								fullStats.KernelModeTime += jobStats.KernelModeTime;
								fullStats.UserModeTime += jobStats.UserModeTime;
								fullStats.WorkingSetPrivate += jobStats.WorkingSetPrivate;
							}
						}

						await jobHandle.PublishStatsAsync( fullStats );
					}
					else
						await jobHandle.PublishStatsAsync( new UsageStatistics() );
				}
			}
			catch ( OperationCanceledException )
			{
			}
			catch ( Exception ex )
			{
				_logger.LogWarning( ex, $"Failed to collect job statistics." );
			}
			finally
			{
				try
				{
					if ( !_cts.IsCancellationRequested )
						await Task.Delay( 3000, _cts.Token );
				}
				catch ( OperationCanceledException )
				{
				}
			}
		}
	}

	// WAS always launches worker processes as: w3wp.exe -ap "PoolName" ...
	private static readonly Regex _appPoolArgumentRegex = new( @"-ap\s+(?:""([^""]+)""|(\S+))", RegexOptions.Compiled );

	private static string? GetAppPoolNameFromCommandLine ( Process w3wpProcess )
	{
		var commandLine = NativeFunctions.GetProcessCommandLine( w3wpProcess );

		if ( string.IsNullOrEmpty( commandLine ) )
			return null;

		var match = _appPoolArgumentRegex.Match( commandLine );

		if ( !match.Success )
			return null;

		return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
	}
}

public interface IManagementLockHandle
{
	ServerManager ServerManager { get; }

	Task JoinTransactionAsync ( Func<Task> action, Func<Task> rollbackAction );
}

internal record struct UsageStatistics ( ulong KernelModeTime, ulong UserModeTime, ulong WorkingSetPrivate );
