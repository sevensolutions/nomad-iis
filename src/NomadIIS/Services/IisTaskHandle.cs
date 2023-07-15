using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Grpc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;

namespace NomadIIS.Services;

public sealed class IisTaskHandle : IDisposable
{
	private readonly ManagementService _owner;
	private readonly CancellationTokenSource _ctsDisposed = new();
	private TaskConfig? _taskConfig;
	private DateTime? _startDate;
	private string? _appPoolName;
	private string? _websiteName;
	private CpuStats _totalCpuStats = new();
	private CpuStats _kernelModeCpuStats = new();
	private CpuStats _userModeCpuStats = new();

	internal IisTaskHandle ( ManagementService owner, string taskId )
	{
		_owner = owner;

		TaskId = taskId;
	}

	public string TaskId { get; }

	public async Task RunAsync ( ILogger<DriverService> logger, TaskConfig task, CancellationToken cancellationToken )
	{
		_taskConfig = task;
		_startDate = DateTime.UtcNow;

		var ports = task.Resources.Ports;

		_appPoolName = GetAppPoolName( task );
		_websiteName = _appPoolName;

		var config = task.MsgpackDriverConfig.DecodeAsTaskConfig();

		var appPath = config.Path;

		if ( !Path.IsPathRooted( appPath ) )
			appPath = Path.Combine( task.AllocDir, task.Name, appPath );

		var bindings = config.Bindings.Select( binding =>
		{
			var port = ports.FirstOrDefault( x => x.Label == binding.PortLabel );
			if ( port is null )
				throw new KeyNotFoundException( $"Couldn't resolve binding-port with label \"{binding.PortLabel}\" in the network config." );

			return new { Binding = binding, PortMapping = port };
		} ).ToArray();

		await _owner.LockAsync( async serverManager =>
		{
			// Create AppPool
			logger.LogInformation( $"Task {task.Id}: Creating AppPool and Website with name {_appPoolName}..." );

			var appPool = FindApplicationPool( serverManager );

			if ( appPool is null )
			{
				appPool = serverManager.ApplicationPools.Add( _appPoolName );
				appPool.AutoStart = true;

				switch ( config.ManagedPipelineMode )
				{
					case "Integrated":
						appPool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
						break;
					case "Classic":
						appPool.ManagedPipelineMode = ManagedPipelineMode.Classic;
						break;
				}

				if ( !string.IsNullOrWhiteSpace( config.ManagedRuntimeVersion ) )
				{
					if ( config.ManagedRuntimeVersion == "None" )
						appPool.ManagedRuntimeVersion = "";
					else
						appPool.ManagedRuntimeVersion = config.ManagedRuntimeVersion;
				}

				switch ( config.StartMode )
				{
					case "OnDemand":
						appPool.StartMode = StartMode.OnDemand;
						break;
					case "AlwaysRunning":
						appPool.StartMode = StartMode.AlwaysRunning;
						break;
				}

				if ( config.IdleTimeout is not null )
					appPool.ProcessModel.IdleTimeout = config.IdleTimeout.Value;

				appPool.Recycling.DisallowOverlappingRotation = config.DisabledOverlappedRecycle;

				if ( config.PeriodicRestart is not null )
					appPool.Recycling.PeriodicRestart.Time = config.PeriodicRestart.Value;

				var envVarsCollection = appPool.GetCollection( "environmentVariables" );

				foreach ( var env in task.Env )
					AddEnvironmentVariable( envVarsCollection, env.Key, env.Value );

				if ( config.EnableUdpLogging )
				{
					var loggingPorts = await SetupUdpStdoutForwarder( logger, task.StdoutPath, cancellationToken );

					AddEnvironmentVariable( envVarsCollection, "NOMAD_STDOUT_UDP_LOCAL_PORT", loggingPorts.LocalPort.ToString() );
					AddEnvironmentVariable( envVarsCollection, "NOMAD_STDOUT_UDP_REMOTE_PORT", loggingPorts.RemotePort.ToString() );
				}

				//var publicStdOutPath = await SetupPipeForwarder( task.StdoutPath, cancellationToken );
			}

			// Create Website
			try
			{
				var website = serverManager.Sites.FirstOrDefault( x => x.Name == _websiteName );
				if ( website is null )
				{
					website = serverManager.Sites.CreateElement();
					website.Id = serverManager.Sites.Count > 0 ? serverManager.Sites.Max( x => x.Id ) + 1 : 0;
					website.Name = _websiteName;
					website.ApplicationDefaults.ApplicationPoolName = appPool.Name;

					var application = website.Applications.FirstOrDefault();
					if ( application is null )
						application = website.Applications.Add( "/", appPath );

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

							await SendTaskEventAsync( $"Using certificate: {certificate.FriendlyName}" );

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

						binding.Protocol = b.Binding.Type;
					}

					serverManager.Sites.Add( website );
				}
			}
			catch ( Exception ex )
			{
				// Try to clean up
				var website = serverManager.Sites.FirstOrDefault( x => x.Name == _websiteName );
				if ( website is not null )
					serverManager.Sites.Remove( website );

				var appPool2 = FindApplicationPool( serverManager );
				if ( appPool2 is not null )
					serverManager.ApplicationPools.Remove( appPool2 );

				await SendTaskEventAsync( $"Error: {ex.Message}" );

				throw;
			}
		} );

		await SendTaskEventAsync( $"Application started, Name: {_appPoolName}" );

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
	public async Task StopAsync ( ILogger<DriverService> logger )
	{
		await _owner.LockAsync( serverManager =>
		{
			var website = serverManager.Sites.FirstOrDefault( x => x.Name == _websiteName );
			var appPool = FindApplicationPool( serverManager );

			if ( website is not null )
			{
				try
				{
					if ( website.State != ObjectState.Stopped )
						website.Stop();
				}
				catch ( Exception ex )
				{
					logger.LogWarning( ex, $"Failed to stop Website {_websiteName}." );
				}

				serverManager.Sites.Remove( website );
			}

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

				serverManager.ApplicationPools.Remove( appPool );
			}

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
		_appPoolName = GetAppPoolName( request.Handle.Config );
		_websiteName = _appPoolName;
	}

	public async Task SignalAsync ( ILogger<DriverService> logger, string signal )
	{
		if ( string.IsNullOrEmpty( signal ) )
			return;

		switch ( signal.ToUpperInvariant() )
		{
			case "SIGHUP":
			case "RECYCLE":
				await _owner.LockAsync( async serverManager =>
				{
					var appPool = GetApplicationPool( serverManager );

					if ( appPool is not null )
					{
						logger.LogInformation( $"Recycle AppPool {_appPoolName}" );

						appPool.Recycle();

						await SendTaskEventAsync( $"ApplicationPool recycled, Name = {_appPoolName}" );
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
		return await _owner.LockAsync( serverManager =>
		{
			var appPool = GetApplicationPool( serverManager );

			var w3wpPids = appPool.WorkerProcesses.Select( x => x.ProcessId ).ToArray();

			if ( w3wpPids.Length > 0 )
			{
				var stats = WmiHelper.QueryWmiStatistics( w3wpPids );

				var totalCpu = _totalCpuStats.Percent( stats.KernelModeTime + stats.UserModeTime );

				return Task.FromResult( new TaskResourceUsage()
				{
					Cpu = new CPUUsage()
					{
						Percent = totalCpu,
						SystemMode = _kernelModeCpuStats.Percent( stats.KernelModeTime ),
						UserMode = _userModeCpuStats.Percent( stats.UserModeTime ),
						TotalTicks = _totalCpuStats.TicksConsumed( totalCpu ),
						MeasuredFields = { CPUUsage.Types.Fields.Percent, CPUUsage.Types.Fields.SystemMode, CPUUsage.Types.Fields.UserMode }
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

	private static string GetAppPoolName ( TaskConfig taskConfig )
		=> $"{taskConfig.AllocId}-{taskConfig.Name}";

	private ApplicationPool GetApplicationPool ( ServerManager serverManager )
		=> FindApplicationPool( serverManager ) ?? throw new KeyNotFoundException( $"No AppPool with name {_appPoolName} found." );
	private ApplicationPool? FindApplicationPool ( ServerManager serverManager )
		=> serverManager.ApplicationPools.FirstOrDefault( x => x.Name == _appPoolName );

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
		catch ( Exception )
		{
		}
	}

	private async Task<(int LocalPort, int RemotePort)> SetupUdpStdoutForwarder ( ILogger<DriverService> logger, string privatePipeName, CancellationToken cancellationToken )
	{
		var localPort = GetAvailablePort(10000);
		var remotePort = GetAvailablePort( localPort + 1 );

		_ = await Task.Factory.StartNew( async () =>
		{
			try
			{
				var nomadPipe = new NamedPipeClientStream( privatePipeName.Replace( "//./pipe/", "" ) );

				await nomadPipe.ConnectAsync();

				while ( !_ctsDisposed.IsCancellationRequested )
				{
					UdpClient? udpClient = null;

					try
					{
						var ipEndpoint = new IPEndPoint( IPAddress.Loopback, remotePort );
						udpClient = new UdpClient( ipEndpoint );

						while ( !_ctsDisposed.IsCancellationRequested )
						{
							var result = await udpClient.ReceiveAsync( _ctsDisposed.Token );

							// Ensure that nobody else is logging to our endpoint
							if ( result.RemoteEndPoint.Port == localPort )
								await nomadPipe.WriteAsync( result.Buffer, _ctsDisposed.Token );
						}
					}
					catch ( Exception ex )
					{
						logger.LogWarning( ex, ex.Message );

						if ( udpClient is not null )
							udpClient.Dispose();
					}
				}
			}
			catch ( Exception ex )
			{
				logger.LogError( ex, ex.Message );
			}
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default );

		return (localPort, remotePort);
	}

	/* TODOPEI: Pipe Version doesn't work. At least not with log4net.
	private async Task<string> SetupPipeForwarder ( string privatePipeName, CancellationToken cancellationToken )
	{
		var pipeName = "testpipe";

		var pipeSecurity = new PipeSecurity();
		pipeSecurity.AddAccessRule(
			new PipeAccessRule( new SecurityIdentifier( WellKnownSidType.WorldSid, null ),
			PipeAccessRights.ReadWrite, AccessControlType.Allow ) );



		_pipeTask = await Task.Factory.StartNew( async () =>
		{
			try
			{
				var nomadPipe = new NamedPipeClientStream( privatePipeName.Replace( "//./pipe/", "" ) );

				await nomadPipe.ConnectAsync();

				var waitingForConnectionText = Encoding.UTF8.GetBytes( "Waiting for connection..." );
				await nomadPipe.WriteAsync( waitingForConnectionText, 0, waitingForConnectionText.Length );

				while ( !_ctsDisposed.IsCancellationRequested )
				{
					var pipeServer = NamedPipeServerStreamAcl.Create( pipeName,
						PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None, 0, 0, pipeSecurity );

					await pipeServer.WaitForConnectionAsync( cancellationToken );

					var connectedText = Encoding.UTF8.GetBytes( " CONNECTED!\n\n" );
					await nomadPipe.WriteAsync( connectedText, 0, connectedText.Length );

					while ( !_ctsDisposed.IsCancellationRequested && pipeServer.IsConnected )
					{
						await pipeServer.CopyToAsync( nomadPipe, _ctsDisposed.Token );
					}

					if ( pipeServer is not null )
						await pipeServer.DisposeAsync();
				}
			}
			catch ( Exception ex )
			{
			}
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default );

		return $"//./pipe/{pipeName}";
	}*/

	private class CpuStats
	{
		private double? _previousCpuTime;
		private DateTime? _previousTimestamp;

		public double Percent ( double cpuTime )
		{
			var now = DateTime.UtcNow;

			if ( _previousCpuTime is null || _previousTimestamp is null )
			{
				_previousCpuTime = cpuTime;
				_previousTimestamp = now;

				return 0d;
			}

			var timeDelta = now.Subtract( _previousTimestamp.Value ).TotalNanoseconds;
			var cpuDelta = cpuTime - _previousCpuTime.Value;

			var result = timeDelta <= 0d || cpuDelta <= 0d ? 0d : cpuDelta / timeDelta * 100d;

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
}
