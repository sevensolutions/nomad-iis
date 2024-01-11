using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using NomadIIS.Services.Grpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

	internal IisTaskHandle ( ManagementService owner, string taskId )
	{
		if ( string.IsNullOrWhiteSpace( taskId ) )
			throw new ArgumentNullException( nameof( taskId ) );

		_owner = owner ?? throw new ArgumentNullException( nameof( owner ) );

		TaskId = taskId;
	}

	public string TaskId { get; }

	public async Task<DriverStateV1> RunAsync ( ILogger<DriverService> logger, TaskConfig task )
	{
		logger.LogInformation( $"Starting task {task.Id} (Alloc: {task.AllocId})..." );

		_state = new DriverStateV1();

		try
		{
			_taskConfig = task;
			_state.StartDate = DateTime.UtcNow;

			_state.AppPoolName = BuildAppPoolOrWebsiteName( task );

			var config = MessagePackHelper.Deserialize<DriverTaskConfig>( task.MsgpackDriverConfig );

			foreach ( var app in config.Applications )
			{
				// In case someone is specifying the alias with a leading slash.
				if ( app.Alias is not null )
					app.Alias.TrimStart( '/' );
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
				// Create AppPool
				var appPool = FindApplicationPool( serverManager, _state.AppPoolName );
				if ( appPool is null )
				{
					logger.LogInformation( $"Task {task.Id}: Creating AppPool with name {_state.AppPoolName}..." );

					appPool = CreateApplicationPool( serverManager, _state.AppPoolName, _taskConfig, config );
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
						CreateApplication( website, appPool, _taskConfig, app );
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
				SetupDirectoryPermissions( logger );

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
			if ( invalidChars.Contains( c ) )
				sb.Append( '_' );
			else
				sb.Append( c );
		}

		return sb.ToString();
	}

	private static ApplicationPool GetApplicationPool ( ServerManager serverManager, string name )
		=> FindApplicationPool( serverManager, name ) ?? throw new KeyNotFoundException( $"No AppPool with name {name} found." );
	private static ApplicationPool? FindApplicationPool ( ServerManager serverManager, string name )
		=> serverManager.ApplicationPools.FirstOrDefault( x => x.Name == name );
	private static ApplicationPool CreateApplicationPool ( ServerManager serverManager, string name, TaskConfig taskConfig, DriverTaskConfig config )
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

		// TODOPEI: Doesn't work because of wrong permission
		//AddEnvironmentVariable( envVarsCollection, "NOMAD_STDOUT_PATH", task.StdoutPath );
		//AddEnvironmentVariable( envVarsCollection, "NOMAD_STDERR_PATH", task.StderrPath );

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
			var port = taskConfig.Resources.Ports.FirstOrDefault( x => x.Label == binding.PortLabel );
			if ( port is null )
				throw new KeyNotFoundException( $"Couldn't resolve binding-port with label \"{binding.PortLabel}\" in the network config." );

			return new { Binding = binding, PortMapping = port };
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
				$"{ipAddress}:{b.PortMapping.Value}:{b.Binding.Hostname}",
				certificateHash, certificateStoreName, sslFlags );

			binding.Protocol = b.Binding.Type.ToString().ToLower();
		}

		serverManager.Sites.Add( website );

		return website;
	}

	private static Application? FindApplicationByPath ( Site website, string path )
		=> website.Applications.FirstOrDefault( x => x.Path == path );
	private static Application CreateApplication ( Site website, ApplicationPool appPool, TaskConfig taskConfig, DriverTaskConfigApplication appConfig )
	{
		var alias = $"/{appConfig.Alias}";
		var physicalPath = appConfig.Path.Replace( '/', '\\' );

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

	private void SetupDirectoryPermissions ( ILogger logger )
	{
		// https://developer.hashicorp.com/nomad/docs/concepts/filesystem
		// https://learn.microsoft.com/en-us/troubleshoot/developer/webapps/iis/www-authentication-authorization/default-permissions-user-rights
		// https://stackoverflow.com/questions/51277338/remove-users-group-permission-for-folder-inside-programdata

#pragma warning disable CA1416 // Plattformkompatibilität überprüfen

		var identity = $"IIS AppPool\\{_state!.AppPoolName}";

		var allocDir = new DirectoryInfo( _taskConfig!.AllocDir );

		var builtinUsersSid = TryGetSid( WellKnownSidType.BuiltinUsersSid, logger );
		var authenticatedUserSid = TryGetSid( WellKnownSidType.AuthenticatedUserSid, logger );

		SetupDirectory( @"alloc", null, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( @"alloc\data", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( @"alloc\logs", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( @"alloc\tmp", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( $@"{_taskConfig.Name}\private", null, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( $@"{_taskConfig.Name}\local", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( $@"{_taskConfig.Name}\secrets", FileSystemRights.Read, InheritanceFlags.ObjectInherit, logger );
		SetupDirectory( $@"{_taskConfig.Name}\tmp", FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, logger );

		void SetupDirectory ( string subDirectory, FileSystemRights? fileSystemRights, InheritanceFlags inheritanceFlags, ILogger logger )
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
			if ( fileSystemRights is not null )
			{
				acl.AddAccessRule( new FileSystemAccessRule(
					identity, fileSystemRights.Value, inheritanceFlags, PropagationFlags.InheritOnly, AccessControlType.Allow ) );
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
