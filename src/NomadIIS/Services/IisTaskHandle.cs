﻿#if MANAGEMENT_API
using CliWrap;
using CliWrap.Buffered;
#endif
using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NomadIIS.Services;

public sealed class IisTaskHandle : IDisposable
{
	public const string AppPoolOrWebsiteNamePrefix = "nomad-";

	private readonly ManagementService _owner;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _ctsDisposed = new();

	// Note: These fields need to be recovered by RecoverState()!
	private TaskConfig? _taskConfig;
	private DriverStateV1? _state;
	private bool _isRecovered;

	private readonly CpuStats _totalCpuStats = new();
	private readonly CpuStats _kernelModeCpuStats = new();
	private readonly CpuStats _userModeCpuStats = new();

	private readonly Channel<UsageStatistics> _statsChannel = Channel.CreateBounded<UsageStatistics>( new BoundedChannelOptions( 1 )
	{
		FullMode = BoundedChannelFullMode.DropOldest
	} );

	// TODO: Maybe i'll re-use this later
#pragma warning disable CS0414 // Dem Feld "IisTaskHandle._appPoolStoppedIntentionally" wurde ein Wert zugewiesen, der aber nie verwendet wird.
	private bool _appPoolStoppedIntentionally = false;
#pragma warning restore CS0414 // Dem Feld "IisTaskHandle._appPoolStoppedIntentionally" wurde ein Wert zugewiesen, der aber nie verwendet wird.

	private NamedPipeClientStream? _stdoutLogStream;

	internal IisTaskHandle ( ManagementService owner, ILogger logger, string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		_owner = owner ?? throw new ArgumentNullException( nameof( owner ) );
		_logger = logger;

		TaskId = taskId;
	}

	public string TaskId { get; }
	public TaskConfig? TaskConfig => _taskConfig;
	public int? UdpLoggerPort => _state?.UdpLoggerPort;
	public string? AppPoolName => _state?.AppPoolName;
	public string? WebsiteName => _state?.WebsiteName;
	public bool IsRecovered => _isRecovered;

	public async Task<DriverStateV1> RunAsync ( TaskConfig task )
	{
		_logger.LogInformation( $"Starting task {task.Id} (Alloc: {task.AllocId})..." );

		_state = new DriverStateV1();

		DriverTaskConfig config;

		try
		{
			_taskConfig = task;
			_state.StartDate = DateTime.UtcNow;

			_state.AppPoolName = BuildAppPoolOrWebsiteName( task );

			config = MessagePackHelper.Deserialize<DriverTaskConfig>( task.MsgpackDriverConfig );

			ValidateAndCoerceTaskConfiguration( config );

			await _owner.LockAsync( async handle =>
			{
				// Get a new port for the UDP logger
				if ( config.EnableUdpLogging )
					_state.UdpLoggerPort = GetAvailablePort( 10000 );

				// Create AppPool
				var appPool = FindApplicationPool( handle.ServerManager, _state.AppPoolName );
				if ( appPool is null )
				{
					_logger.LogInformation( $"Task {task.Id}: Creating AppPool with name {_state.AppPoolName}..." );

					appPool = CreateApplicationPool( handle.ServerManager, _state.AppPoolName, _taskConfig, config, _state.UdpLoggerPort, _owner.UdpLoggerPort );
				}

				// Create Website
				Site? website = null;

				if ( !string.IsNullOrEmpty( config.TargetWebsite ) )
				{
					website = FindWebsiteByName( handle.ServerManager, config.TargetWebsite );

					if ( website is null )
						throw new KeyNotFoundException( $"The specified target_website \"{config.TargetWebsite}\" does not exist. Make sure you constrain the job to nodes containing the specified target_website." );

					_state.WebsiteName = website.Name;
					_state.TaskOwnsWebsite = false;

					_logger.LogInformation( $"Task {task.Id}: Using target website with name {_state.WebsiteName}..." );
				}
				else
				{
					_state.WebsiteName = _state.AppPoolName;
					_state.TaskOwnsWebsite = true;

					website = FindWebsiteByName( handle.ServerManager, _state.WebsiteName );

					if ( website is null )
					{
						_logger.LogInformation( $"Task {task.Id}: Creating Website with name {_state.WebsiteName}..." );

						website = await CreateWebsiteAsync( handle, _state.WebsiteName, _taskConfig, config, appPool );
					}
				}

				// Create applications
				foreach ( var app in config.Applications.OrderBy( x => string.IsNullOrEmpty( x.Alias ) ? 0 : 1 ) )
				{
					var application = FindApplicationByPath( website, $"/{app.Alias}" );

					if ( application is not null && !_state.TaskOwnsWebsite )
						throw new InvalidOperationException( $"An application with alias {app.Alias} already exists in website {website.Name}." );

					if ( application is null )
						CreateApplication( website, appPool, _taskConfig, app, _owner );
				}

				_state.ApplicationAliases = config.Applications.Select( x => x.Alias ).ToList();

				return Task.CompletedTask;
			} );
		}
		catch ( Exception ex )
		{
			await SendTaskEventAsync( $"Error: {ex.Message}" );

			try
			{
				await StopAndCleanupAsync();
			}
			catch ( Exception exCleanup )
			{
				_logger.LogWarning( exCleanup, "Failed to start the application and we also failed to cleanup." );
			}

			throw;
		}

		try
		{
			if ( _owner.DirectorySecurity )
				await SetupDirectoryPermissions( config );

			await SendTaskEventAsync( $"Application started, Name: {_state.AppPoolName}" );
		}
		catch ( Exception ex )
		{
			await SendTaskEventAsync( $"Error: {ex.Message}" );

			// Note: We do not rethrow here because the website has already been set-up.
		}

		return _state;
	}

	private void ValidateAndCoerceTaskConfiguration ( DriverTaskConfig config )
	{
		foreach ( var app in config.Applications )
		{
			// In case someone is specifying the alias with a leading slash.
			if ( app.Alias is not null )
				app.Alias = app.Alias.TrimStart( '/' );
		}

		if ( config.Applications.Select( x => x.Alias ).Distinct().Count() != config.Applications.Length )
			throw new ArgumentException( "Every application alias must be unique." );

		if ( !string.IsNullOrEmpty( config.TargetWebsite ) )
		{
			if ( !IsAllowedTargetWebsite( config.TargetWebsite ) )
				throw new InvalidOperationException( $"Using target_website \"{config.TargetWebsite}\" is not allowed on this node." );

			if ( config.TargetWebsite.StartsWith( AppPoolOrWebsiteNamePrefix ) )
				throw new InvalidOperationException( $"Re-using the existing nomad website \"{config.TargetWebsite}\" as target_website is not allowed." );

			if ( config.Applications.Any( x => string.IsNullOrEmpty( x.Alias ) ) )
				throw new ArgumentException( "Defining a root application with an empty alias is not allowed when using a target_website." );
		}
	}

	public async Task StopAsync ()
	{
		if ( _state is null || _taskConfig is null )
			throw new InvalidOperationException( "Invalid state." );

		_logger.LogInformation( $"Stopping task {_taskConfig.Id} (Alloc: {_taskConfig.AllocId})..." );

		await StopAndCleanupAsync();
	}
	public async Task DestroyAsync ()
	{
		if ( _state is null || _taskConfig is null )
			throw new InvalidOperationException( "Invalid state." );

		_logger.LogInformation( $"Destroying task {_taskConfig.Id} (Alloc: {_taskConfig.AllocId})..." );

		await StopAndCleanupAsync();
	}

	private async Task StopAndCleanupAsync ()
	{
		if ( _state is null || _taskConfig is null )
			throw new InvalidOperationException( "Invalid state." );

		HashSet<string>? certificatesToUninstall = null;

		await _owner.LockAsync( handle =>
		{
			var website = _state.WebsiteName is not null ? FindWebsiteByName( handle.ServerManager, _state.WebsiteName ) : null;
			var appPool = _state.AppPoolName is not null ? FindApplicationPool( handle.ServerManager, _state.AppPoolName ) : null;

			if ( appPool is not null )
			{
				try
				{
					if ( appPool.State != ObjectState.Stopped )
						appPool.Stop();
				}
				catch ( Exception ex )
				{
					_logger.LogWarning( ex, $"Failed to stop AppPool {_state.AppPoolName}. Will be removed anyway." );
				}
			}

			if ( website is not null )
			{
				try
				{
					// Check which certificates can be uninstalled. (where usage-count is 1, only this website)
					certificatesToUninstall = website.Bindings
						.Where( x => x.CertificateHash is not null )
						.GroupBy( x => x.CertificateHash )
						.Where( x => x.Count() == 1 )
						.Select( x => Convert.ToHexString( x.Key ) )
						.ToHashSet();
				}
				catch ( Exception ex )
				{
					_logger.LogError( ex, "Failed to detect certificates to uninstall." );
				}

				if ( !_state.TaskOwnsWebsite )
				{
					// Just remove the applications
					if ( _state.ApplicationAliases is not null )
					{
						foreach ( var appAlias in _state.ApplicationAliases )
						{
							var application = FindApplicationByPath( website, $"/{appAlias}" );
							if ( application is not null )
								website.Applications.Remove( application );
						}
					}
					else
						_logger.LogWarning( "Invalid state. Missing _applicationAliases." );
				}
				else
				{
					// Remove the entire site
					handle.ServerManager.Sites.Remove( website );
				}
			}

			if ( appPool is not null )
				handle.ServerManager.ApplicationPools.Remove( appPool );

			return Task.CompletedTask;
		} );

		try
		{
			if ( certificatesToUninstall is not null )
				await CertificateHelper.UninstallCertificatesAsync( certificatesToUninstall );
		}
		catch ( Exception ex )
		{
			_logger.LogError( ex, $"Failed to uninstall certificates of ${_state.WebsiteName}" );
		}
	}

	public void RecoverState ( RecoverTaskRequest request )
	{
		// Note: request.TaskId is null/empty here.
		// Also request.Handle.Config.MsgpackDriverConfig is allways empty.

		_taskConfig = request.Handle.Config;

		_logger.LogInformation( $"Recovering task {_taskConfig.Id} (Alloc: {_taskConfig.AllocId})..." );

		if ( request.Handle.DriverState is not null && !request.Handle.DriverState.IsEmpty )
		{
			if ( request.Handle.Version >= 1 )
				_state = MessagePackSerializer.Deserialize<DriverStateV1>( request.Handle.DriverState.Memory );
			else
				throw new InvalidOperationException( "Invalid state." );
		}
		else
			throw new InvalidOperationException( "Invalid state." );

		_isRecovered = true;

		_logger.LogInformation( $"Recovered task {_taskConfig.Id} from state: {_state}" );
	}

	public async Task SignalAsync ( string signal )
	{
		if ( string.IsNullOrEmpty( signal ) )
			return;

		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		switch ( signal.ToUpperInvariant() )
		{
			case "START":
				await StartAppPoolAsync();
				break;
			case "STOP":
				await StopAppPoolAsync();
				break;

			case "SIGHUP":
			case "RECYCLE":
				await RecycleAppPoolAsync();
				break;

			case "SIGINT":
			case "SIGKILL":
				await StopAsync();
				break;

			default:
				_logger.LogInformation( $"Unsupported signal {signal} received." );
				break;
		}
	}
	public async Task<int> WaitAsync ()
	{
		var tcs = new TaskCompletionSource();

		// TODO: Right now, i have no idea how to monitor the app pool state without producing a memory leak.

		_ctsDisposed.Token.Register( () => tcs.SetResult() );

		await tcs.Task;

		return 0;
	}

	internal ValueTask PublishStatsAsync ( UsageStatistics statistics )
		=> _statsChannel.Writer.WriteAsync( statistics, _ctsDisposed.Token );

	public async Task<TaskResourceUsage> GetStatisticsAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		var stats = await _statsChannel.Reader.ReadAsync( _ctsDisposed.Token );

		var cpuShares = _taskConfig?.Resources.AllocatedResources.Cpu.CpuShares ?? 0L;

		var totalCpu = _totalCpuStats.Percent( stats.KernelModeTime + stats.UserModeTime );

		return new TaskResourceUsage()
		{
			Cpu = new CPUUsage()
			{
				Percent = totalCpu,
				SystemMode = _kernelModeCpuStats.Percent( stats.KernelModeTime ),
				UserMode = _userModeCpuStats.Percent( stats.UserModeTime ),
				TotalTicks = ( cpuShares / 100d ) * totalCpu, // _totalCpuStats.TicksConsumed( totalCpu ),
				MeasuredFields = { CPUUsage.Types.Fields.Percent, CPUUsage.Types.Fields.SystemMode, CPUUsage.Types.Fields.UserMode, CPUUsage.Types.Fields.TotalTicks }
			},
			Memory = new MemoryUsage()
			{
				Rss = stats.WorkingSetPrivate,
				MeasuredFields = { MemoryUsage.Types.Fields.Rss }
			}
		};
	}

	public Task<InspectTaskResponse> InspectAsync ()
	{
		if ( _state is null || _taskConfig is null )
			throw new InvalidOperationException( "Invalid state." );

		return Task.FromResult( new InspectTaskResponse()
		{
			Driver = new TaskDriverStatus()
			{
				Attributes =
				{
					{ "AppPoolName", _state.AppPoolName },
					{ "WebsiteName", _state.WebsiteName }
				}
			},
			Task = new Hashicorp.Nomad.Plugins.Drivers.Proto.TaskStatus()
			{
				Id = _taskConfig.Id,
				Name = _taskConfig.Name,
				StartedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime( _state.StartDate ),
				State = TaskState.Running
			}
		} );
	}

	public void Dispose ()
	{
		_ctsDisposed.Cancel();

		if ( _stdoutLogStream is not null )
			_stdoutLogStream.Dispose();

		_owner.Delete( this );
	}

	#region IIS Helper Methods

	private static string BuildAppPoolOrWebsiteName ( TaskConfig taskConfig )
	{
		var rawName = $"{AppPoolOrWebsiteNamePrefix}{taskConfig.AllocId}-{taskConfig.Name}";

		var invalidChars = ApplicationPoolCollection.InvalidApplicationPoolNameCharacters()
			.Union( SiteCollection.InvalidSiteNameCharacters() )
			.ToArray();

		var sb = new StringBuilder();

		foreach ( var c in rawName )
		{
			if ( invalidChars.Contains( c ) || c == ' ' )
				sb.Append( '_' );
			else
				sb.Append( c );
		}

		var finalName = sb.ToString();

		// AppPool name limit is 64 characters. Website's doesn't seem to have a limit.
		if ( finalName.Length > 64 )
			finalName = $"{AppPoolOrWebsiteNamePrefix}{taskConfig.AllocId}";

		return finalName;
	}

	private static ApplicationPool GetApplicationPool ( ServerManager serverManager, string name )
		=> FindApplicationPool( serverManager, name ) ?? throw new KeyNotFoundException( $"No AppPool with name {name} found." );
	private static ApplicationPool? FindApplicationPool ( ServerManager serverManager, string name )
		=> serverManager.ApplicationPools.FirstOrDefault( x => x.Name == name );
	private static ApplicationPool CreateApplicationPool ( ServerManager serverManager, string name, TaskConfig taskConfig, DriverTaskConfig config, int? udpLocalPort, int? udpRemotePort )
	{
		var appPool = serverManager.ApplicationPools.Add( name );
		appPool.AutoStart = true;

		if ( config.ManagedPipelineMode is not null )
			appPool.ManagedPipelineMode = config.ManagedPipelineMode.Value;

		if ( !string.IsNullOrWhiteSpace( config.ManagedRuntimeVersion ) )
		{
			if ( config.ManagedRuntimeVersion == "None" )
				appPool.ManagedRuntimeVersion = "";
			else if ( config.ManagedRuntimeVersion == "v4.0" )
				appPool.ManagedRuntimeVersion = "v4.0";
			else if ( config.ManagedRuntimeVersion == "v2.0" )
				appPool.ManagedRuntimeVersion = "v2.0";
			else
				throw new ArgumentException( $"Invalid managed_runtime_version. Must be either v4.0, v2.0 or None." );
		}

		if ( config.StartMode is not null )
			appPool.StartMode = config.StartMode.Value;

		if ( config.IdleTimeout is not null )
			appPool.ProcessModel.IdleTimeout = config.IdleTimeout.Value;

		if ( config.DisabledOverlappedRecycle is not null )
			appPool.Recycling.DisallowOverlappingRotation = config.DisabledOverlappedRecycle.Value;

		if ( config.Enable32BitAppOnWin64 is not null )
			appPool.Enable32BitAppOnWin64 = config.Enable32BitAppOnWin64.Value;

		if ( config.PeriodicRestart is not null )
			appPool.Recycling.PeriodicRestart.Time = config.PeriodicRestart.Value;

		if ( config.ServiceUnavailableResponse is not null )
			appPool.Failure.LoadBalancerCapabilities = config.ServiceUnavailableResponse.Value;

		if ( config.QueueLength is not null )
			appPool.QueueLength = config.QueueLength.Value;

		if ( config.StartTimeLimit is not null )
			appPool.ProcessModel.StartupTimeLimit = config.StartTimeLimit.Value;
		if ( config.ShutdownTimeLimit is not null )
			appPool.ProcessModel.ShutdownTimeLimit = config.ShutdownTimeLimit.Value;

		var envVarsCollection = appPool.GetCollection( "environmentVariables" );

		foreach ( var env in GetTaskConfigEnvironmentVariables( taskConfig ) )
			AddEnvironmentVariable( envVarsCollection, env.Key, env.Value );

		if ( udpLocalPort is not null && udpRemotePort is not null )
		{
			AddEnvironmentVariable( envVarsCollection, "NOMAD_STDOUT_UDP_REMOTE_PORT", udpRemotePort.Value.ToString() );
			AddEnvironmentVariable( envVarsCollection, "NOMAD_STDOUT_UDP_LOCAL_PORT", udpLocalPort.Value.ToString() );
		}

		return appPool;

		void AddEnvironmentVariable ( ConfigurationElementCollection envVarsCollection, string key, string value )
		{
			if ( !string.IsNullOrEmpty( key ) && !string.IsNullOrEmpty( value ) )
			{
				var envVarElement = envVarsCollection.CreateElement( "add" );

				envVarElement["name"] = key;
				envVarElement["value"] = value;

				envVarsCollection.Add( envVarElement );
			}
		}
	}

	private static IEnumerable<KeyValuePair<string, string>> GetTaskConfigEnvironmentVariables ( TaskConfig taskConfig )
	{
		var processVariables = Environment
			.GetEnvironmentVariables( EnvironmentVariableTarget.Process )
			.Keys.Cast<string>().ToHashSet();

		var userVariables = new Dictionary<string, string>();

		foreach ( var nomadEnv in taskConfig.Env )
		{
			if ( !string.IsNullOrEmpty( nomadEnv.Key ) && !processVariables.Contains( nomadEnv.Key ) )
				userVariables[nomadEnv.Key] = nomadEnv.Value;
		}

		// Overwrite the temp directory variables to use nomad's own tmp directory acc. to: https://developer.hashicorp.com/nomad/docs/concepts/filesystem
		var tempPath = Path.GetFullPath( Path.Combine( taskConfig.AllocDir, taskConfig.Name, "tmp" ) );
		if ( !Directory.Exists( tempPath ) )
			Directory.CreateDirectory( tempPath );

		userVariables["TMP"] = tempPath;
		userVariables["TEMP"] = tempPath;

		return userVariables.ToArray();
	}

	private static Site? FindWebsiteByName ( ServerManager serverManager, string name )
		=> serverManager.Sites.FirstOrDefault( x => x.Name == name );
	private async Task<Site> CreateWebsiteAsync ( IManagementLockHandle handle, string name, TaskConfig taskConfig, DriverTaskConfig config, ApplicationPool appPool )
	{
		var bindings = config.Bindings.Select( binding =>
		{
			int? port;
			PortMapping? portMapping = null;

			if ( int.TryParse( binding.Port, out var staticPort ) )
			{
				if ( string.IsNullOrEmpty( binding.Hostname ) )
					throw new Exception( $"Static port {staticPort} can only be used in combination with the hostname-setting. You can also use a network stanza to specify the port." );

				port = staticPort;
			}
			else
			{
				portMapping = taskConfig.Resources.Ports.FirstOrDefault( x => x.Label == binding.Port );
				if ( portMapping is null )
					throw new KeyNotFoundException( $"Couldn't resolve binding-port with label \"{binding.Port}\" in the network config." );

				port = portMapping.Value;
			}

			return new { Binding = binding, Port = port, PortMapping = portMapping };
		} ).ToArray();

		var website = handle.ServerManager.Sites.CreateElement();

		website.Id = GetNextAvailableWebsiteId( handle.ServerManager );
		website.Name = name;
		website.ApplicationDefaults.ApplicationPoolName = appPool.Name;

		website.Applications.Clear();

		if ( !config.Applications.Any( x => string.IsNullOrEmpty( x.Alias ) ) )
			website.Applications.Add( "/", @"C:\inetpub\wwwroot" );

		foreach ( var b in bindings )
		{
			(X509Certificate2 Certificate, string? StoreName)? usedCertificate = null;
			var sslFlags = SslFlags.None;

			if ( b.Binding.Type == DriverTaskConfigBindingType.Https )
			{
				var certificateBlock = b.Binding.Certificates.FirstOrDefault();
				if ( certificateBlock is null )
					throw new ArgumentException( "Missing certificate configuration for HTTPS binding." );

				if ( !string.IsNullOrEmpty( certificateBlock.Thumbprint ) )
				{
					usedCertificate = await CertificateHelper.FindCertificateByThumbprintAsync( certificateBlock.Thumbprint );

					if ( usedCertificate is null )
						throw new KeyNotFoundException( $"Couldn't find certificate with hash {certificateBlock.Thumbprint}." );
				}
				else
				{
					// Install the Certificate.
					// This is done by joining the IIS Commit-Transaction which means:
					// If committing the IIS changes will fail, it will also rollback the certificate installation.
					await handle.JoinTransactionAsync( async () =>
					{
						if ( !string.IsNullOrEmpty( certificateBlock.PfxFile ) )
						{
							var certificateFilePath = certificateBlock.PfxFile;

							// If the path is not an absolute path, make it relative to the task-directory.
							if ( !Path.IsPathRooted( certificateFilePath ) )
								certificateFilePath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, certificateFilePath );

							if ( !File.Exists( certificateFilePath ) )
								throw new FileNotFoundException( $"Couldn't find certificate file {certificateFilePath}." );

							usedCertificate = await CertificateHelper.InstallPfxCertificateAsync(
								certificateFilePath, certificateBlock.Password );

							if ( usedCertificate is null )
								throw new Exception( $"Failed to install certificate because it wasn't found after install. Maybe it's not valid anymore?" );

							await SendTaskEventAsync( $"Installed certificate: {usedCertificate.Value.Certificate.Thumbprint}" );
						}
						else if ( !string.IsNullOrEmpty( certificateBlock.CertFile ) || !string.IsNullOrEmpty( certificateBlock.KeyFile ) )
						{
							if ( string.IsNullOrEmpty( certificateBlock.CertFile ) || string.IsNullOrEmpty( certificateBlock.KeyFile ) )
								throw new ArgumentException( $"When using pem encoded certificates, cert_file and key_file must be specified in combination." );

							var certificateFilePath = certificateBlock.CertFile;
							var keyFilePath = certificateBlock.KeyFile;

							// If the path is not an absolute path, make it relative to the task-directory.
							if ( !Path.IsPathRooted( certificateFilePath ) )
								certificateFilePath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, certificateFilePath );
							if ( !Path.IsPathRooted( keyFilePath ) )
								keyFilePath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, keyFilePath );

							usedCertificate = await CertificateHelper.InstallPemCertificateAsync(
								certificateFilePath, keyFilePath );

							if ( usedCertificate is null )
								throw new Exception( $"Failed to install certificate because it wasn't found after install. Maybe it's not valid anymore?" );

							await SendTaskEventAsync( $"Installed certificate: {usedCertificate.Value.Certificate.Thumbprint}" );
						}
						else if ( certificateBlock.UseSelfSigned )
						{
							var temFile = Path.GetTempFileName() + ".pfx";
							var randomPassword = Guid.NewGuid().ToString( "N" );

							var selfSignedCertificate = CertificateHelper.GenerateSelfSignedCertificate(
								b.Binding.Hostname ?? "localhost", TimeSpan.FromDays( 365 ), temFile, randomPassword );

							usedCertificate = await CertificateHelper.InstallPfxCertificateAsync( temFile, randomPassword );

							await SendTaskEventAsync( $"Installed self-signed certificate: {usedCertificate.Value.Certificate.Thumbprint}" );
						}
						else
							throw new ArgumentException( $"No certificate has been specified for the HTTPS binding on port {b.Port}." );
					}, async () =>
					{
						if ( usedCertificate?.Certificate?.Thumbprint is not null )
							await CertificateHelper.UninstallCertificatesAsync( new HashSet<string>( [usedCertificate.Value.Certificate.Thumbprint] ) );
					} );
				}

				if ( b.Binding.RequireSni is not null && b.Binding.RequireSni.Value )
					sslFlags |= SslFlags.Sni;
			}

			var ipAddress = b.Binding.IPAddress ?? "*";

			// Note: Certificate needs to be specified in this Add() method. Otherwise it doesn't work.
			var binding = website.Bindings.Add(
				$"{ipAddress}:{b.Port}:{b.Binding.Hostname}",
				usedCertificate?.Certificate.GetCertHash(), usedCertificate?.StoreName, sslFlags );

			binding.Protocol = b.Binding.Type.ToString().ToLower();
		}

		handle.ServerManager.Sites.Add( website );

		return website;
	}

	private static Application? FindApplicationByPath ( Site website, string path )
		=> website.Applications.FirstOrDefault( x => x.Path == path );
	private static Application CreateApplication ( Site website, ApplicationPool appPool, TaskConfig taskConfig, DriverTaskConfigApplication appConfig, ManagementService managementService )
	{
		if ( string.IsNullOrEmpty( appConfig.Path ) )
			throw new ArgumentNullException( $"Missing application path." );

		var alias = $"/{appConfig.Alias}";
		var physicalPath = appConfig.Path.Replace( '/', '\\' );

		// If the path is not an absolute path, make it relative to the task-directory.
		if ( !Path.IsPathRooted( physicalPath ) )
			physicalPath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, physicalPath );

		var diPhysicalPath = new DirectoryInfo( physicalPath );

		// Check if we want to use a placeholder app
		if ( Directory.Exists( managementService.PlaceholderAppPath ) )
		{
			var directoryIsNew = false;

			if ( !diPhysicalPath.Exists )
			{
				diPhysicalPath.Create();
				directoryIsNew = true;
			}

			// If the directory is new or empty, copy the placeholder app
			if ( directoryIsNew || !diPhysicalPath.EnumerateFileSystemInfos().Any() )
				FileSystemHelper.CopyDirectory( managementService.PlaceholderAppPath, diPhysicalPath.FullName );
		}

		var application = website.Applications.Add( alias, diPhysicalPath.FullName );

		application.ApplicationPoolName = appPool.Name;

		if ( appConfig.EnablePreload is not null )
			application.SetAttributeValue( "preloadEnabled", appConfig.EnablePreload.Value );

		if ( appConfig.VirtualDirectories is not null )
		{
			if ( appConfig.VirtualDirectories.Select( x => x.Alias ).Distinct().Count() != appConfig.VirtualDirectories.Length )
				throw new ArgumentException( "Every virtual_directory alias must be unique." );

			foreach ( var vdir in appConfig.VirtualDirectories )
			{
				var physicalVdirPath = vdir.Path.Replace( '/', '\\' );

				// If the path is not an absolute path, make it relative to the task-directory.
				if ( !Path.IsPathRooted( physicalVdirPath ) )
					physicalVdirPath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, physicalVdirPath );

				application.VirtualDirectories.Add( $"/{vdir.Alias}", physicalVdirPath );
			}
		}

		return application;
	}

	#endregion

	private bool IsAllowedTargetWebsite ( string websiteName )
	{
		foreach ( var allowed in _owner.AllowedTargetWebsites )
		{
			if ( allowed == "*" || allowed == websiteName )
				return true;

			// TODO: Should we support regex?
		}

		return false;
	}

	private async Task SendTaskEventAsync ( string message )
	{
		if ( _taskConfig is null )
			return;

		try
		{
			await _owner.SendEventAsync( new DriverTaskEvent()
			{
				Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime( DateTime.UtcNow ),
				AllocId = _taskConfig.AllocId,
				TaskId = _taskConfig.Id,
				TaskName = _taskConfig.Name,
				Message = message
			} );
		}
		catch ( Exception ex )
		{
			_logger?.LogError( ex, "Failed to send task event." );
		}
	}

	private async Task SetupDirectoryPermissions ( DriverTaskConfig config )
	{
		try
		{
			// GH-43: It may happen that this throws an IdentityNotMappedException sometimes.
			// I think setting up the AppPoolIdentity takes some time.
			// So if we're too early, we try again in 2 seconds.
			SetupDirectoryPermissionsCore( config );
		}
		catch ( IdentityNotMappedException ex )
		{
			_logger.LogDebug( ex, "Failed to setup directory permissions for allocation {allocation}. Retrying in 2 seconds...", _taskConfig?.AllocId );

			await Task.Delay( 2000 );

			SetupDirectoryPermissionsCore( config );
		}
	}
	private void SetupDirectoryPermissionsCore ( DriverTaskConfig config )
	{
		// https://developer.hashicorp.com/nomad/docs/concepts/filesystem
		// https://learn.microsoft.com/en-us/troubleshoot/developer/webapps/iis/www-authentication-authorization/default-permissions-user-rights
		// https://stackoverflow.com/questions/51277338/remove-users-group-permission-for-folder-inside-programdata

		var appPoolIdentity = $"IIS AppPool\\{_state!.AppPoolName}";

		string[] identities = config.PermitIusr ? [appPoolIdentity, "IUSR"] : [appPoolIdentity];

		var allocDir = new DirectoryInfo( _taskConfig!.AllocDir );

		var builtinUsersSid = TryGetSid( WellKnownSidType.BuiltinUsersSid );
		var authenticatedUserSid = TryGetSid( WellKnownSidType.AuthenticatedUserSid );

		SetupDirectory( [appPoolIdentity], @"alloc", null, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );
		SetupDirectory( [appPoolIdentity], @"alloc\data", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );
		SetupDirectory( [appPoolIdentity], @"alloc\logs", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );
		SetupDirectory( [appPoolIdentity], @"alloc\tmp", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );
		SetupDirectory( null, $@"{_taskConfig.Name}\private", null, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );
		SetupDirectory( identities, $@"{_taskConfig.Name}\local", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );
		SetupDirectory( [appPoolIdentity], $@"{_taskConfig.Name}\secrets", FileSystemRights.Read, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly );
		SetupDirectory( [appPoolIdentity], $@"{_taskConfig.Name}\tmp", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None );

		void SetupDirectory ( string[]? identities, string subDirectory, FileSystemRights? fileSystemRights, InheritanceFlags inheritanceFlags, PropagationFlags propagationFlags )
		{
			var directory = allocDir;

			if ( subDirectory != "\\" )
				directory = new DirectoryInfo( Path.Combine( directory.FullName, subDirectory ) );

			if ( !directory.Exists )
				return;

			var acl = directory.GetAccessControl();

			// Disable Inheritance and copy existing rules
			acl.SetAccessRuleProtection( true, true );
			directory.SetAccessControl( acl );

			// Re-read the ACL
			acl = directory.GetAccessControl();

			// Remove unwanted BuiltIn-users/groups which allow access to everyone
			foreach ( FileSystemAccessRule rule in acl.GetAccessRules( true, false, typeof( SecurityIdentifier ) ) )
			{
				if ( ( builtinUsersSid is not null && rule.IdentityReference == builtinUsersSid ) ||
					( ( authenticatedUserSid is not null ) && rule.IdentityReference == authenticatedUserSid ) )
					acl.RemoveAccessRule( rule );
			}

			// Add new Rules
			if ( identities is not null && identities.Length > 0 && fileSystemRights is not null )
			{
				foreach ( var identity in identities )
				{
					acl.AddAccessRule( new FileSystemAccessRule(
						identity, fileSystemRights.Value, inheritanceFlags, propagationFlags, AccessControlType.Allow ) );
				}
			}

			// Apply the new ACL
			directory.SetAccessControl( acl );
		}
	}

	private SecurityIdentifier? TryGetSid ( WellKnownSidType sidType )
	{
		try
		{
			return new SecurityIdentifier( sidType, null );
		}
		catch ( Exception ex )
		{
			_logger.LogWarning( ex, $"Failed to get SID {sidType}." );

			return null;
		}
	}

	private static long GetNextAvailableWebsiteId ( ServerManager serverManager )
	{
		var usedIds = serverManager.Sites
			.Select( x => x.Id )
			.ToHashSet();

		for ( var id = 0L; id < long.MaxValue - 1; id++ )
		{
			var next = id + 1;

			if ( !usedIds.Contains( next ) )
				return next;
		}

		throw new Exception( "No more website IDs available." );
	}



	internal async Task ShipLogsAsync ( byte[] data )
	{
		if ( _taskConfig?.StdoutPath is null )
			return;

		if ( _stdoutLogStream is null || !_stdoutLogStream.IsConnected )
		{
			if ( _stdoutLogStream is not null )
				await _stdoutLogStream.DisposeAsync();

			_stdoutLogStream = new NamedPipeClientStream( _taskConfig.StdoutPath.Replace( "//./pipe/", "" ) );

			await _stdoutLogStream.ConnectAsync();
		}

		await _stdoutLogStream.WriteAsync( data, _ctsDisposed.Token );
	}

	private static int GetAvailablePort ( int startingPort )
	{
		var portArray = new List<int>();

		var properties = IPGlobalProperties.GetIPGlobalProperties();

		// Ignore active connections
		var connections = properties.GetActiveTcpConnections();
		portArray.AddRange( from n in connections
							where n.LocalEndPoint.Port >= startingPort
							select n.LocalEndPoint.Port );

		// Ignore active TCP listners
		var endPoints = properties.GetActiveTcpListeners();
		portArray.AddRange( from n in endPoints
							where n.Port >= startingPort
							select n.Port );

		// Ignore active UDP listeners
		endPoints = properties.GetActiveUdpListeners();
		portArray.AddRange( from n in endPoints
							where n.Port >= startingPort
							select n.Port );

		portArray.Sort();

		for ( var i = startingPort; i < ushort.MaxValue; i++ )
			if ( !portArray.Contains( i ) )
				return i;

		return 0;
	}

	public async Task StartAppPoolAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( async handle =>
		{
			var appPool = GetApplicationPool( handle.ServerManager, _state.AppPoolName );

			try
			{
				if ( appPool.State == ObjectState.Stopped )
					appPool.Start();
			}
			catch ( COMException )
			{
				// Sometimes, restarting the pool too fast doesn't work.
				// So we wait a bit and try again.
				await Task.Delay( 2000 );
				if ( appPool.State == ObjectState.Stopped )
					appPool.Start();
			}

			_appPoolStoppedIntentionally = false;

			return Task.CompletedTask;
		} );

		await SendTaskEventAsync( $"ApplicationPool started, Name = {_state.AppPoolName}" );
	}
	public async Task StopAppPoolAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( handle =>
		{
			var appPool = GetApplicationPool( handle.ServerManager, _state.AppPoolName );

			_appPoolStoppedIntentionally = true;
			if ( appPool.State == ObjectState.Started )
				appPool.Stop();

			return Task.CompletedTask;
		} );

		await SendTaskEventAsync( $"ApplicationPool stopped, Name = {_state.AppPoolName}" );
	}
	public async Task RecycleAppPoolAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( handle =>
		{
			var appPool = GetApplicationPool( handle.ServerManager, _state.AppPoolName );

			appPool.Recycle();

			return Task.CompletedTask;
		} );

		await SendTaskEventAsync( $"ApplicationPool recycled, Name = {_state.AppPoolName}" );
	}

	#region Management API Methods
#if MANAGEMENT_API
	public async Task<NomadIIS.ManagementApi.ApiModel.TaskStatusResponse> GetStatusAsync ()
	{
		if ( _state is null || _taskConfig is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		return await _owner.LockAsync( handle =>
		{
			var appPool = GetApplicationPool( handle.ServerManager, _state.AppPoolName );

			var isWorkerProcessRunning = appPool.WorkerProcesses.Any(
				x => x.State == WorkerProcessState.Starting || x.State == WorkerProcessState.Running );

			return Task.FromResult( new NomadIIS.ManagementApi.ApiModel.TaskStatusResponse()
			{
				AllocId = _taskConfig.AllocId,
				TaskName = _taskConfig.Name,
				ApplicationPool = new NomadIIS.ManagementApi.ApiModel.ApplicationPool()
				{
					Status = (NomadIIS.ManagementApi.ApiModel.ApplicationPoolStatus)(int)appPool.State,
					IsWorkerProcessRunning = isWorkerProcessRunning
				}
			} );
		} );
	}

	public async Task DownloadFileAsync ( HttpResponse response, string path )
	{
		if ( _state is null || _taskConfig is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		path = FileSystemHelper.SanitizeRelativePath( path );

		var physicalPath = Path.Combine( _taskConfig.AllocDir, _taskConfig.Name, path );

		if ( File.Exists( physicalPath ) )
		{
			response.Headers.ContentType = "application/octet-stream";
			response.Headers.ContentDisposition = $"attachment; filename=\"{Path.GetFileName( physicalPath )}\"";
			response.StatusCode = StatusCodes.Status200OK;

			await response.StartAsync();

			using var fs = File.OpenRead( physicalPath );
			await fs.CopyToAsync( response.Body );
		}
		else
		{
			response.Headers.ContentType = "application/zip";
			response.Headers.ContentDisposition = $"attachment; filename=\"{Path.GetFileName( physicalPath )}.zip\"";
			response.StatusCode = StatusCodes.Status200OK;

			await response.StartAsync();

			ZipFile.CreateFromDirectory( physicalPath, response.Body );
		}
	}
	public async Task UploadFileAsync ( Stream stream, bool isZip, string path, bool hot, bool cleanFolder )
	{
		if ( stream is null )
			throw new ArgumentNullException( nameof( stream ) );

		if ( _state is null || _taskConfig is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		path = FileSystemHelper.SanitizeRelativePath( path );

		try
		{
			if ( !hot )
			{
				await StopAppPoolAsync();
				await Task.Delay( 500 );
			}

			var physicalPath = Path.Combine( _taskConfig.AllocDir, _taskConfig.Name, path );

			if ( cleanFolder )
				FileSystemHelper.CleanFolder( physicalPath );

			if ( isZip )
			{
				using var archive = new ZipArchive( stream );
				archive.ExtractToDirectory( physicalPath );
			}
			else
			{
				using var fs = File.OpenWrite( physicalPath );
				await stream.CopyToAsync( fs );
			}
		}
		finally
		{
			if ( !hot )
				await StartAppPoolAsync();
		}
	}
	public Task DeleteFileAsync ( string path )
	{
		if ( _state is null || _taskConfig is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		path = FileSystemHelper.SanitizeRelativePath( path );

		string physicalPath;

		if ( path.EndsWith( "/*" ) || path.EndsWith( "/*.*" ) )
		{
			if ( path.EndsWith( "/*" ) )
				path = path[..^2];
			else
				path = path[..^4];

			physicalPath = Path.Combine( _taskConfig.AllocDir, _taskConfig.Name, path );
			FileSystemHelper.CleanFolder( physicalPath );
			return Task.CompletedTask;
		}

		physicalPath = Path.Combine( _taskConfig.AllocDir, _taskConfig.Name, path );

		if ( File.Exists( physicalPath ) )
			File.Delete( physicalPath );
		else
			Directory.Delete( physicalPath, true );

		return Task.CompletedTask;
	}

	public async Task<byte[]?> TakeScreenshotAsync ( string path = "/", CancellationToken cancellationToken = default )
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		if ( string.IsNullOrEmpty( path ) || !path.StartsWith( '/' ) )
			path = $"/{path}";

		var port = await _owner.LockAsync( handle =>
		{
			var site = handle.ServerManager.Sites.First( x => x.Name == _state.WebsiteName );

			var httpBinding = site.Bindings.FirstOrDefault( x => x.Protocol == "http" )?.EndPoint;

			return Task.FromResult( httpBinding?.Port );
		}, cancellationToken );

		if ( port is null )
			return null;

		return await PlaywrightHelper.TakeScreenshotAsync( $"http://localhost:{port}{path}", cancellationToken );
	}

	public async Task TakeProcessDump ( FileInfo targetFile, CancellationToken cancellationToken = default )
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		if ( string.IsNullOrEmpty( _owner.ProcdumpBinaryPath ) )
			throw new NotSupportedException( $"Missing procdump_binary_path in driver configuration." );
		if ( !File.Exists( _owner.ProcdumpBinaryPath ) )
			throw new NotSupportedException( $"{_owner.ProcdumpBinaryPath} is not available." );

		if ( !_owner.ProcdumpEulaAccepted )
			throw new InvalidOperationException( "Procdump EULA has not been accepted." );

		var w3wpPids = await _owner.LockAsync( handle =>
		{
			var appPool = GetApplicationPool( handle.ServerManager, _state.AppPoolName );

			return Task.FromResult( appPool.WorkerProcesses.Select( x => x.ProcessId ).ToArray() );
		}, cancellationToken );

		if ( w3wpPids is null || w3wpPids.Length == 0 )
			throw new InvalidOperationException( "No w3wp process running." );

		var pid = w3wpPids[0];

		var procdump = Cli.Wrap( _owner.ProcdumpBinaryPath )
			.WithArguments( x => x
				.Add( "-accepteula" )
				.Add( "-ma" )
				.Add( pid )
				.Add( targetFile.FullName ) )
			.WithValidation( CommandResultValidation.None );

		cancellationToken.ThrowIfCancellationRequested();

		var result = await procdump.ExecuteBufferedAsync( cancellationToken );

		if ( !targetFile.Exists || targetFile.Length == 0 )
			throw new Exception( result.StandardOutput + Environment.NewLine + result.StandardError );
	}

#endif
	#endregion

	private class CpuStats
	{
		private double? _previousCpuTime;
		private DateTime? _previousTimestamp;

		public double Percent ( double cpuTime )
		{
			// https://github.com/hashicorp/nomad/blob/main/client/lib/cpustats/stats.go

			var now = DateTime.UtcNow;

			if ( _previousCpuTime is null || _previousTimestamp is null )
			{
				_previousCpuTime = cpuTime;
				_previousTimestamp = now;

				return 0d;
			}

			var timeDelta = now.Subtract( _previousTimestamp.Value ).TotalMilliseconds;
			var cpuDelta = cpuTime - _previousCpuTime.Value;

			//var result = timeDelta <= 0d || cpuDelta <= 0d ? 0d : (cpuDelta / timeDelta) * 100d;

			// https://stackoverflow.com/questions/22195277/get-the-cpu-usage-of-each-process-from-wmi
			var result = cpuDelta / ( timeDelta * 100d * Environment.ProcessorCount );

			_previousCpuTime = cpuTime;
			_previousTimestamp = now;

			return result;
		}

		public ulong TicksConsumed ( double percent )
		{
			return 0UL; // TODO

			//return ( percent / 100d ) * shelpers.TotalTicksAvailable() / Environment.ProcessorCount
		}
	}
}
