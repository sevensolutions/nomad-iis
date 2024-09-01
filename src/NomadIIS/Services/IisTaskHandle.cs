#if MANAGEMENT_API
using CliWrap;
using CliWrap.Buffered;
#endif
using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using NomadIIS.Services.Grpc;
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
using System.Threading.Tasks;

namespace NomadIIS.Services;

public sealed class IisTaskHandle : IDisposable
{
	private readonly ManagementService _owner;
	private readonly CancellationTokenSource _ctsDisposed = new();

	// Note: These fields need to be recovered by RecoverState()!
	private TaskConfig? _taskConfig;
	private DriverStateV1? _state;

	private readonly CpuStats _totalCpuStats = new();
	private readonly CpuStats _kernelModeCpuStats = new();
	private readonly CpuStats _userModeCpuStats = new();

	private bool _appPoolStoppedIntentionally = false;

	private NamedPipeClientStream? _stdoutLogStream;

	internal IisTaskHandle ( ManagementService owner, string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		_owner = owner ?? throw new ArgumentNullException( nameof( owner ) );

		TaskId = taskId;
	}

	public string TaskId { get; }
	public TaskConfig? TaskConfig => _taskConfig;
	public int? UdpLoggerPort => _state?.UdpLoggerPort;

	public async Task<DriverStateV1> RunAsync ( ILogger<DriverService> logger, TaskConfig task )
	{
		logger.LogInformation( $"Starting task {task.Id} (Alloc: {task.AllocId})..." );

		_state = new DriverStateV1();

		DriverTaskConfig config;

		try
		{
			_taskConfig = task;
			_state.StartDate = DateTime.UtcNow;

			_state.AppPoolName = BuildAppPoolOrWebsiteName( task );

			config = MessagePackHelper.Deserialize<DriverTaskConfig>( task.MsgpackDriverConfig );

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

				if ( config.TargetWebsite.StartsWith( "nomad-" ) )
					throw new InvalidOperationException( $"Re-using the existing nomad website \"{config.TargetWebsite}\" as target_website is not allowed." );

				if ( config.Applications.Any( x => string.IsNullOrEmpty( x.Alias ) ) )
					throw new ArgumentException( "Defining a root application with an empty alias is not allowed when using a target_website." );
			}

			await _owner.LockAsync( serverManager =>
			{
				// Get a new port for the UDP logger
				if ( config.EnableUdpLogging )
					_state.UdpLoggerPort = GetAvailablePort( 10000 );

				// Create AppPool
				var appPool = FindApplicationPool( serverManager, _state.AppPoolName );
				if ( appPool is null )
				{
					logger.LogInformation( $"Task {task.Id}: Creating AppPool with name {_state.AppPoolName}..." );

					appPool = CreateApplicationPool( serverManager, _state.AppPoolName, _taskConfig, config, _state.UdpLoggerPort, _owner.UdpLoggerPort );
				}

				// Create Website
				Site? website = null;

				if ( !string.IsNullOrEmpty( config.TargetWebsite ) )
				{
					website = FindWebsiteByName( serverManager, config.TargetWebsite );

					if ( website is null )
						throw new KeyNotFoundException( $"The specified target_website \"{config.TargetWebsite}\" does not exist. Make sure you constrain the job to nodes containing the specified target_website." );

					_state.WebsiteName = website.Name;
					_state.TaskOwnsWebsite = false;

					logger.LogInformation( $"Task {task.Id}: Using target website with name {_state.WebsiteName}..." );
				}
				else
				{
					_state.WebsiteName = _state.AppPoolName;
					_state.TaskOwnsWebsite = true;

					website = FindWebsiteByName( serverManager, _state.WebsiteName );

					if ( website is null )
					{
						logger.LogInformation( $"Task {task.Id}: Creating Website with name {_state.WebsiteName}..." );

						website = CreateWebsite( serverManager, _state.WebsiteName, _taskConfig, config, appPool );
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
			await SendTaskEventAsync( logger, $"Error: {ex.Message}" );

			throw;
		}

		try
		{
			if ( _owner.DirectorySecurity )
				await SetupDirectoryPermissions( logger, config );

			await SendTaskEventAsync( logger, $"Application started, Name: {_state.AppPoolName}" );
		}
		catch ( Exception ex )
		{
			await SendTaskEventAsync( logger, $"Error: {ex.Message}" );

			// Note: We do not rethrow here because the website has already been set-up.
		}

		return _state;
	}
	public async Task StopAsync ( ILogger<DriverService> logger )
	{
		if ( _state is null || _taskConfig is null || string.IsNullOrEmpty( _state.AppPoolName ) || string.IsNullOrEmpty( _state.WebsiteName ) )
			throw new InvalidOperationException( "Invalid state." );

		logger.LogInformation( $"Stopping task {_taskConfig.Id} (Alloc: {_taskConfig.AllocId})..." );

		await _owner.LockAsync( serverManager =>
		{
			var website = FindWebsiteByName( serverManager, _state.WebsiteName );
			var appPool = FindApplicationPool( serverManager, _state.AppPoolName );

			if ( appPool is not null )
			{
				try
				{
					if ( appPool.State != ObjectState.Stopped )
						appPool.Stop();
				}
				catch ( Exception ex )
				{
					logger.LogWarning( ex, $"Failed to stop AppPool {_state.AppPoolName}." );
				}
			}

			if ( website is not null )
			{
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
						logger.LogWarning( "Invalid state. Missing _applicationAliases." );
				}
				else
				{
					// Remove the entire site
					serverManager.Sites.Remove( website );
				}
			}

			if ( appPool is not null )
				serverManager.ApplicationPools.Remove( appPool );

			return Task.CompletedTask;
		} );
	}
	public async Task DestroyAsync ( ILogger<DriverService> logger )
	{
		await StopAsync( logger );
	}

	public void RecoverState ( ILogger logger, RecoverTaskRequest request )
	{
		// Note: request.TaskId is null/empty here.
		// Also request.Handle.Config.MsgpackDriverConfig is allways empty.

		_taskConfig = request.Handle.Config;

		logger.LogInformation( $"Recovering task {_taskConfig.Id} (Alloc: {_taskConfig.AllocId})..." );

		if ( request.Handle.DriverState is not null && !request.Handle.DriverState.IsEmpty )
		{
			if ( request.Handle.Version >= 1 )
				_state = MessagePackSerializer.Deserialize<DriverStateV1>( request.Handle.DriverState.Memory );
			else
				throw new InvalidOperationException( "Invalid state." );
		}
		else
			throw new InvalidOperationException( "Invalid state." );

		logger.LogInformation( $"Recovered task {_taskConfig.Id} from state: {_state}" );
	}

	public async Task SignalAsync ( ILogger<DriverService> logger, string signal )
	{
		if ( string.IsNullOrEmpty( signal ) )
			return;

		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		switch ( signal.ToUpperInvariant() )
		{
			case "SIGHUP":
			case "RECYCLE":
				await _owner.LockAsync( async serverManager =>
				{
					var appPool = GetApplicationPool( serverManager, _state.AppPoolName );

					if ( appPool is not null )
					{
						logger.LogInformation( $"Recycle AppPool {_state.AppPoolName}" );

						appPool.Recycle();

						await SendTaskEventAsync( logger, $"ApplicationPool recycled, Name = {_state.AppPoolName}" );
					}
				} );

				break;

			case "SIGINT":
			case "SIGKILL":
				await StopAsync( logger );
				break;

			default:
				logger.LogInformation( $"Unsupported signal {signal} received." );
				break;
		}
	}
	public async Task<int> WaitAsync ( ILogger<DriverService> logger )
	{
		var exitCode = 0;

		try
		{
			while ( !_ctsDisposed.IsCancellationRequested )
			{
				await Task.Delay( 3000, _ctsDisposed.Token );

				if ( _state is null )
				{
					exitCode = -1;
					break;
				}

				exitCode = await _owner.LockAsync( serverManager =>
				{
					var appPool = FindApplicationPool( serverManager, _state.AppPoolName );

					if ( _appPoolStoppedIntentionally )
						return Task.FromResult( 0 );

					if ( appPool is not null )
					{
						logger.LogDebug( $"AppPool {_state.AppPoolName} is in state {appPool.State}" );

						if ( appPool.State == ObjectState.Stopped )
							return Task.FromResult( -1 );
					}
					else
						return Task.FromResult( -1 );

					return Task.FromResult( 0 );
				} );

				if ( exitCode != 0 )
					break;
			}
		}
		catch ( OperationCanceledException )
		{
		}

		return exitCode;
	}

	public async Task<TaskResourceUsage> GetStatisticsAsync ( ILogger<DriverService> logger )
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		return await _owner.LockAsync( serverManager =>
		{
			var appPool = GetApplicationPool( serverManager, _state.AppPoolName );

			var w3wpPids = appPool.WorkerProcesses.Select( x => x.ProcessId ).ToArray();

			if ( w3wpPids.Length > 0 )
			{
				var stats = WmiHelper.QueryWmiStatistics( w3wpPids );

				var cpuShares = _taskConfig?.Resources.AllocatedResources.Cpu.CpuShares ?? 0L;

				var totalCpu = _totalCpuStats.Percent( stats.KernelModeTime + stats.UserModeTime );

				return Task.FromResult( new TaskResourceUsage()
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
				} );
			}
			else
			{
				return Task.FromResult( new TaskResourceUsage() );
			}
		} );
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
		var rawName = $"nomad-{taskConfig.AllocId}-{taskConfig.Name}";

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
			finalName = $"nomad-{taskConfig.AllocId}";

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

		appPool.Recycling.DisallowOverlappingRotation = config.DisabledOverlappedRecycle;

		if ( config.PeriodicRestart is not null )
			appPool.Recycling.PeriodicRestart.Time = config.PeriodicRestart.Value;

		var envVarsCollection = appPool.GetCollection( "environmentVariables" );

		foreach ( var env in taskConfig.Env )
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

	private static Site? FindWebsiteByName ( ServerManager serverManager, string name )
		=> serverManager.Sites.FirstOrDefault( x => x.Name == name );
	private static Site CreateWebsite ( ServerManager serverManager, string name, TaskConfig taskConfig, DriverTaskConfig config, ApplicationPool appPool )
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

		var website = serverManager.Sites.CreateElement();

		website.Id = GetNextAvailableWebsiteId( serverManager );
		website.Name = name;
		website.ApplicationDefaults.ApplicationPoolName = appPool.Name;

		website.Applications.Clear();

		if ( !config.Applications.Any( x => string.IsNullOrEmpty( x.Alias ) ) )
			website.Applications.Add( "/", @"C:\inetpub\wwwroot" );

		foreach ( var b in bindings )
		{
			string? certificateStoreName = null;
			byte[]? certificateHash = null;

			if ( b.Binding.CertificateHash is not null )
			{
				using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

				store.Open( OpenFlags.ReadOnly );

				var certificate = store.Certificates
					.FirstOrDefault( x => x.GetCertHashString().Equals( b.Binding.CertificateHash, StringComparison.InvariantCultureIgnoreCase ) );

				if ( certificate is null )
					throw new KeyNotFoundException( $"Couldn't find certificate with hash {b.Binding.CertificateHash}." );

				certificateStoreName = store.Name;
				certificateHash = certificate.GetCertHash();
			}

			var sslFlags = SslFlags.None;
			if ( b.Binding.RequireSni is not null && b.Binding.RequireSni.Value )
				sslFlags |= SslFlags.Sni;

			var ipAddress = b.Binding.IPAddress ?? "*";

			// Note: Certificate needs to be specified in this Add() method. Otherwise it doesn't work.
			var binding = website.Bindings.Add(
				$"{ipAddress}:{b.Port}:{b.Binding.Hostname}",
				certificateHash, certificateStoreName, sslFlags );

			binding.Protocol = b.Binding.Type.ToString().ToLower();
		}

		serverManager.Sites.Add( website );

		return website;
	}

	private static Application? FindApplicationByPath ( Site website, string path )
		=> website.Applications.FirstOrDefault( x => x.Path == path );
	private static Application CreateApplication ( Site website, ApplicationPool appPool, TaskConfig taskConfig, DriverTaskConfigApplication appConfig, ManagementService managementService )
	{
		var alias = $"/{appConfig.Alias}";

		var pathIsSet = !string.IsNullOrEmpty( appConfig.Path );

		var physicalPath = appConfig.Path?.Replace( '/', '\\' );

		if ( !pathIsSet )
		{
			// If the user didn't specify a path we create a local subfolder for the app
			// and copy a placeholder into it.
			physicalPath = "local";

			if ( alias == "/" )
				physicalPath = Path.Combine( physicalPath, "root" );
			else
				physicalPath = Path.Combine( physicalPath, alias.TrimStart( '/' ).Replace( '/', '_' ) );

			Directory.CreateDirectory( physicalPath );
		}

		if ( !Path.IsPathRooted( physicalPath ) )
			physicalPath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, physicalPath! );

		// Copy a placeholder app
		if ( !pathIsSet && !string.IsNullOrEmpty( managementService.PlaceholderAppPath ) && Directory.Exists( managementService.PlaceholderAppPath ) )
			FileSystemHelper.CopyDirectory( managementService.PlaceholderAppPath, physicalPath );

		if ( !Path.IsPathRooted( physicalPath ) )
			physicalPath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, physicalPath );

		var application = website.Applications.Add( alias, physicalPath );

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

	private async Task SendTaskEventAsync ( ILogger logger, string message )
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
			logger.LogError( ex, "Failed to send task event." );
		}
	}

	private async Task SetupDirectoryPermissions ( ILogger logger, DriverTaskConfig config )
	{
		try
		{
			// GH-43: It may happen that this throws an IdentityNotMappedException sometimes.
			// I think setting up the AppPoolIdentity takes some time.
			// So if we're too early, we try again in 2 seconds.
			SetupDirectoryPermissionsCore( logger, config );
		}
		catch ( IdentityNotMappedException ex )
		{
			logger.LogDebug( ex, "Failed to setup directory permissions for allocation {allocation}. Retrying in 2 seconds...", _taskConfig?.AllocId );

			await Task.Delay( 2000 );

			SetupDirectoryPermissionsCore( logger, config );
		}
	}
	private void SetupDirectoryPermissionsCore ( ILogger logger, DriverTaskConfig config )
	{
		// https://developer.hashicorp.com/nomad/docs/concepts/filesystem
		// https://learn.microsoft.com/en-us/troubleshoot/developer/webapps/iis/www-authentication-authorization/default-permissions-user-rights
		// https://stackoverflow.com/questions/51277338/remove-users-group-permission-for-folder-inside-programdata

#pragma warning disable CA1416 // Plattformkompatibilität überprüfen

		var appPoolIdentity = $"IIS AppPool\\{_state!.AppPoolName}";

		string[] identities = config.PermitIusr ? [appPoolIdentity, "IUSR"] : [appPoolIdentity];

		var allocDir = new DirectoryInfo( _taskConfig!.AllocDir );

		var builtinUsersSid = TryGetSid( WellKnownSidType.BuiltinUsersSid, logger );
		var authenticatedUserSid = TryGetSid( WellKnownSidType.AuthenticatedUserSid, logger );

		SetupDirectory( [appPoolIdentity], @"alloc", null, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );
		SetupDirectory( [appPoolIdentity], @"alloc\data", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );
		SetupDirectory( [appPoolIdentity], @"alloc\logs", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );
		SetupDirectory( [appPoolIdentity], @"alloc\tmp", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );
		SetupDirectory( null, $@"{_taskConfig.Name}\private", null, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );
		SetupDirectory( identities, $@"{_taskConfig.Name}\local", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );
		SetupDirectory( [appPoolIdentity], $@"{_taskConfig.Name}\secrets", FileSystemRights.Read, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, logger );
		SetupDirectory( [appPoolIdentity], $@"{_taskConfig.Name}\tmp", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, logger );

		void SetupDirectory ( string[]? identities, string subDirectory, FileSystemRights? fileSystemRights, InheritanceFlags inheritanceFlags, PropagationFlags propagationFlags, ILogger logger )
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

#pragma warning restore CA1416 // Plattformkompatibilität überprüfen
	}

	private SecurityIdentifier? TryGetSid ( WellKnownSidType sidType, ILogger logger )
	{
#pragma warning disable CA1416 // Plattformkompatibilität überprüfen
		try
		{
			return new SecurityIdentifier( sidType, null );
		}
		catch ( Exception ex )
		{
			logger.LogWarning( ex, $"Failed to get SID {sidType}." );

			return null;
		}
#pragma warning restore CA1416 // Plattformkompatibilität überprüfen
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



	internal async Task ShipLogsAsync ( ILogger logger, byte[] data )
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

	#region Management API Methods
#if MANAGEMENT_API
	public async Task<bool> IsAppPoolRunning ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		return await _owner.LockAsync( serverManager =>
		{
			var appPool = GetApplicationPool( serverManager, _state.AppPoolName );

			var w3wpPids = appPool.WorkerProcesses.Select( x => x.ProcessId ).ToArray();

			return Task.FromResult( w3wpPids.Length > 0 );
		} );
	}

	public async Task StartAppPoolAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( async serverManager =>
		{
			var appPool = serverManager.ApplicationPools.First( x => x.Name == _state.AppPoolName );

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
	}
	public async Task StopAppPoolAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( serverManager =>
		{
			var appPool = serverManager.ApplicationPools.First( x => x.Name == _state.AppPoolName );

			_appPoolStoppedIntentionally = true;
			if ( appPool.State == ObjectState.Started )
				appPool.Stop();

			return Task.CompletedTask;
		} );
	}
	public async Task RecycleAppPoolAsync ()
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( serverManager =>
		{
			var appPool = serverManager.ApplicationPools.First( x => x.Name == _state.AppPoolName );

			appPool.Recycle();

			return Task.CompletedTask;
		} );
	}

	public async Task UploadAsync ( Stream stream, string appAlias = "/" )
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		try
		{
			var physicalPath = await _owner.LockAsync( serverManager =>
			{
				var appPool = serverManager.ApplicationPools.First( x => x.Name == _state.AppPoolName );

				var site = serverManager.Sites.First( x => x.Name == _state.WebsiteName );

				var app = site.Applications.FirstOrDefault( a => a.Path == appAlias );
				if ( app is null )
					throw new KeyNotFoundException( $"App {appAlias} not found." );

				var virtualRoot = app.VirtualDirectories.Where( v => v.Path == "/" ).First();

				var physicalPath = virtualRoot.PhysicalPath;

				_appPoolStoppedIntentionally = true;
				if ( appPool.State == ObjectState.Started )
					appPool.Stop();

				return Task.FromResult( physicalPath );
			} );

			await Task.Delay( 500 );

			FileSystemHelper.CleanFolder( physicalPath! );

			using var archive = new ZipArchive( stream );

			archive.ExtractToDirectory( physicalPath! );
		}
		finally
		{
			await _owner.LockAsync( async serverManager =>
			{
				var appPool = serverManager.ApplicationPools.First( x => x.Name == _state.AppPoolName );

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
			} );
		}
	}

	public async Task<byte[]?> TakeScreenshotAsync ( string appAlias = "/" )
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		var port = await _owner.LockAsync( serverManager =>
		{
			var site = serverManager.Sites.First( x => x.Name == _state.WebsiteName );

			var httpBinding = site.Bindings.FirstOrDefault( x => x.Protocol == "http" )?.EndPoint;

			return Task.FromResult( httpBinding?.Port );
		} );

		if ( port is null )
			return null;

		return await PlaywrightHelper.TakeScreenshotAsync( $"http://localhost:{port}{appAlias}" );
	}

	public async Task<FileInfo> TakeProcessDump ( string appAlias = "/", CancellationToken cancellationToken = default )
	{
		if ( _state is null || string.IsNullOrEmpty( _state.AppPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		var procdumpExePath = @"C:\procdump.exe";

		// TODO: Make configurable
		if ( !File.Exists( procdumpExePath ) )
			throw new NotSupportedException( "C:\\procdump.exe is not available." );

		var w3wpPids = await _owner.LockAsync( serverManager =>
		{
			var appPool = GetApplicationPool( serverManager, _state.AppPoolName );

			return Task.FromResult( appPool.WorkerProcesses.Select( x => x.ProcessId ).ToArray() );
		} );

		if ( w3wpPids is null || w3wpPids.Length == 0 )
			throw new InvalidOperationException( "No w3wp process running." );

		var pid = w3wpPids[0];

		var dumpFile = new FileInfo( Path.GetTempFileName() );

		var procdump = Cli.Wrap( procdumpExePath )
			.WithArguments( x => x
				.Add( "-accepteula" )
				.Add( "-ma" )
				.Add( pid )
				.Add( dumpFile.FullName ) )
			.WithValidation( CommandResultValidation.None );

		var result = await procdump.ExecuteBufferedAsync( cancellationToken );

		// Add the .dmp extension
		dumpFile = new FileInfo( Path.Combine( dumpFile.DirectoryName!, $"{dumpFile.Name}.dmp" ) );

		if ( !dumpFile.Exists || dumpFile.Length == 0 )
		{
			throw new Exception( result.StandardOutput + Environment.NewLine + result.StandardError );
		}

		return dumpFile;
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
