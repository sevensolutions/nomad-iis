using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using NomadIIS.Services.Grpc;
using System;
using System.Collections.Generic;
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
	private DriverTaskConfig? _config;
	private DateTime? _startDate;
	private string? _appPoolName;
	private string? _websiteName;

	private readonly CpuStats _totalCpuStats = new();
	private readonly CpuStats _kernelModeCpuStats = new();
	private readonly CpuStats _userModeCpuStats = new();

	internal IisTaskHandle ( ManagementService owner, string taskId )
	{
		_owner = owner;

		TaskId = taskId;
	}

	public string TaskId { get; }

	public async Task RunAsync ( ILogger<DriverService> logger, TaskConfig task )
	{
		try
		{
			_taskConfig = task;
			_startDate = DateTime.UtcNow;

			_appPoolName = BuildAppPoolOrWebsiteName( task );

			_config = MessagePackReader.Deserialize<DriverTaskConfig>( task.MsgpackDriverConfig );

			if ( _config.Applications.Select( x => x.Alias ).Distinct().Count() != _config.Applications.Length )
				throw new ArgumentException( "Every application alias must be unique." );

			if ( !string.IsNullOrEmpty( _config.TargetWebsite ) && _config.Applications.Any( x => string.IsNullOrEmpty( x.Alias ) ) )
				throw new ArgumentException( "Defining a root application with an empty alias is not allowed when using a target_website." );

			await _owner.LockAsync( serverManager =>
			{
				// Create AppPool
				logger.LogInformation( $"Task {task.Id}: Creating AppPool with name {_appPoolName}..." );

				var appPool = FindApplicationPool( serverManager, _appPoolName );
				if ( appPool is null )
					appPool = CreateApplicationPool( serverManager, _appPoolName, _taskConfig, _config );

				// Create Website
				Site? website = null;

				if ( !string.IsNullOrEmpty( _config.TargetWebsite ) )
				{
					website = FindWebsiteByName( serverManager, _config.TargetWebsite );

					if ( website is null )
						throw new KeyNotFoundException( $"The specified target_website \"{_config.TargetWebsite}\" does not exist. Make sure you constrain the job to nodes containing the specified target_website." );

					_websiteName = website.Name;

					logger.LogInformation( $"Task {task.Id}: Using target website with name {_websiteName}..." );
				}
				else
				{
					_websiteName = _appPoolName;

					logger.LogInformation( $"Task {task.Id}: Creating Website with name {_websiteName}..." );

					website = FindWebsiteByName( serverManager, _websiteName );

					if ( website is null )
						website = CreateWebsite( serverManager, _websiteName, _taskConfig, _config, appPool );
				}

				// Create applications
				foreach ( var app in _config.Applications.OrderBy( x => string.IsNullOrEmpty( x.Alias ) ? 0 : 1 ) )
				{
					var application = FindApplicationByPath( website, $"/{app.Alias}" );

					if ( application is not null )
						throw new InvalidOperationException( $"An application with alias {app.Alias} already exists in website {website.Name}." );

					CreateApplication( website, appPool, _taskConfig, app );
				}

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

			await SendTaskEventAsync( logger, $"Application started, Name: {_appPoolName}" );
		}
		catch ( Exception ex )
		{
			await SendTaskEventAsync( logger, $"Error: {ex.Message}" );

			// Note: We do not rethrow here because the website has already been set-up.
		}
	}
	public async Task StopAsync ( ILogger<DriverService> logger )
	{
		if ( string.IsNullOrEmpty( _appPoolName ) || string.IsNullOrEmpty( _websiteName ) || _config is null )
			throw new InvalidOperationException( "Invalid state." );

		await _owner.LockAsync( serverManager =>
		{
			var website = FindWebsiteByName( serverManager, _websiteName );
			var appPool = FindApplicationPool( serverManager, _appPoolName );

			if ( appPool is not null )
			{
				try
				{
					if ( appPool.State != ObjectState.Stopped )
						appPool.Stop();
				}
				catch ( Exception ex )
				{
					logger.LogWarning( ex, $"Failed to stop AppPool {_appPoolName}." );
				}
			}

			if ( website is not null )
			{
				if ( !string.IsNullOrEmpty( _config.TargetWebsite ) )
				{
					// Just remove the applications
					foreach(var app in _config.Applications)
					{
						var application = FindApplicationByPath( website, $"/{app.Alias}" );
						if ( application is not null )
							website.Applications.Remove( application );
					}
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

	public void RecoverState ( RecoverTaskRequest request )
	{
		_taskConfig = request.Handle.Config;
		_appPoolName = BuildAppPoolOrWebsiteName( _taskConfig );

		_config = MessagePackReader.Deserialize<DriverTaskConfig>( _taskConfig.MsgpackDriverConfig );

		if ( !string.IsNullOrEmpty( _config.TargetWebsite ) )
			_websiteName = _config.TargetWebsite;
		else
			_websiteName = _appPoolName;

		// TODOPEI: Recover _startDate
		_startDate = DateTime.UtcNow;
	}

	public async Task SignalAsync ( ILogger<DriverService> logger, string signal )
	{
		if ( string.IsNullOrEmpty( signal ) )
			return;

		if ( string.IsNullOrEmpty( _appPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		switch ( signal.ToUpperInvariant() )
		{
			case "SIGHUP":
			case "RECYCLE":
				await _owner.LockAsync( async serverManager =>
				{
					var appPool = GetApplicationPool( serverManager, _appPoolName );

					if ( appPool is not null )
					{
						logger.LogInformation( $"Recycle AppPool {_appPoolName}" );

						appPool.Recycle();

						await SendTaskEventAsync( logger, $"ApplicationPool recycled, Name = {_appPoolName}" );
					}
				} );

				break;

			default:
				logger.LogInformation( $"Unsupported signal {signal} received." );
				break;
		}
	}
	public async Task<int> WaitAsync ( ILogger<DriverService> logger )
	{
		try
		{
			while ( !_ctsDisposed.IsCancellationRequested )
			{
				await Task.Delay( 3000, _ctsDisposed.Token );

				//await _owner.LockAsync( serverManager =>
				//{
				//	var appPool = FindApplicationPool( serverManager );

				//	if ( appPool is not null )
				//	{
				//		logger.LogDebug( $"AppPool {_appPoolName} is in state {appPool.State}" );
				//		//if ( appPool.State != ObjectState.Started )
				//		//	return -1;
				//	}

				//	return Task.CompletedTask;
				//} );
			}
		}
		catch ( OperationCanceledException )
		{
		}

		return 0;
	}

	public async Task<TaskResourceUsage> GetStatisticsAsync ( ILogger<DriverService> logger )
	{
		if ( string.IsNullOrEmpty( _appPoolName ) )
			throw new InvalidOperationException( "Invalid state." );

		return await _owner.LockAsync( serverManager =>
		{
			var appPool = GetApplicationPool( serverManager, _appPoolName );

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
		if ( _taskConfig is null || _startDate is null )
			throw new InvalidOperationException( "Invalid state." );

		return Task.FromResult( new InspectTaskResponse()
		{
			Driver = new TaskDriverStatus()
			{
				Attributes =
				{
					{ "AppPoolName", _appPoolName },
					{ "WebsiteName", _websiteName }
				}
			},
			Task = new Hashicorp.Nomad.Plugins.Drivers.Proto.TaskStatus()
			{
				Id = _taskConfig.Id,
				Name = _taskConfig.Name,
				StartedAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime( _startDate.Value ),
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
		var rawName = $"{taskConfig.AllocId}-{taskConfig.Name}";

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

		website.Id = serverManager.Sites.Count > 0 ? serverManager.Sites.Max( x => x.Id ) + 1 : 1;
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
		var physicalPath = appConfig.Path;

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
				var physicalVdirPath = vdir.Path;

				if ( !Path.IsPathRooted( physicalVdirPath ) )
					physicalVdirPath = Path.Combine( taskConfig.AllocDir, taskConfig.Name, physicalVdirPath );

				application.VirtualDirectories.Add( $"/{vdir.Alias}", physicalVdirPath );
			}
		}

		return application;
	}

	#endregion

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

		var identity = $"IIS AppPool\\{_appPoolName}";

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
