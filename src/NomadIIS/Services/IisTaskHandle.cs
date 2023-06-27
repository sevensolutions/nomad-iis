﻿using Hashicorp.Nomad.Plugins.Drivers.Proto;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Grpc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NomadIIS.Services
{
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

		public async Task<IisTaskHandleState> RunAsync ( ILogger<DriverService> logger, TaskConfig task )
		{
			_taskConfig = task;
			_startDate = DateTime.UtcNow;

			var ports = task.Resources.Ports;

			_appPoolName = $"nomad-{Guid.NewGuid():N}";
			_websiteName = _appPoolName;

			var config = task.MsgpackDriverConfig.DecodeAsTaskConfig();

			var appPath = config.Path;

			if ( !Path.IsPathRooted( appPath ) )
				appPath = Path.Combine( task.AllocDir, appPath );

			var bindings = config.Bindings.Select( binding =>
			{
				var port = ports.FirstOrDefault( x => x.Label == binding.PortLabel );
				if ( port is null )
					throw new KeyNotFoundException( $"Couldn't resolve binding-port with label \"{binding.PortLabel}\" in the network config." );

				return new { Type = binding.Type, PortMapping = port };
			} ).ToArray();

			await _owner.LockAsync( serverManager =>
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
						appPool.ManagedRuntimeVersion = config.ManagedRuntimeVersion;

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

					AddEnvironmentVariable( envVarsCollection, "NOMAD_STDOUT_PATH", task.StdoutPath );
					AddEnvironmentVariable( envVarsCollection, "NOMAD_STDERR_PATH", task.StderrPath );
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
							website.Bindings.Add( $"*:{b.PortMapping.Value}:", b.Type );

						serverManager.Sites.Add( website );
					}
				}
				catch ( Exception )
				{
					// Try to clean up
					var website = serverManager.Sites.FirstOrDefault( x => x.Name == _websiteName );
					if ( website is not null )
						serverManager.Sites.Remove( website );

					var appPool2 = FindApplicationPool( serverManager );
					if ( appPool2 is not null )
						serverManager.ApplicationPools.Remove( appPool2 );

					throw;
				}

				return Task.CompletedTask;
			} );

			await SendTaskEventAsync( $"Application started, Name: {_appPoolName}" );

			return new IisTaskHandleState()
			{
				AppPoolName = _appPoolName,
				WebsiteName = _websiteName
			};

			void AddEnvironmentVariable( ConfigurationElementCollection envVarsCollection, string key, string value )
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
					if ( website.State != ObjectState.Stopped )
						website.Stop();

					serverManager.Sites.Remove( website );
				}

				if ( appPool is not null )
				{
					if ( appPool.State != ObjectState.Stopped )
						appPool.Stop();

					serverManager.ApplicationPools.Remove( appPool );
				}

				return Task.CompletedTask;
			} );
		}
		public async Task DestroyAsync ( ILogger<DriverService> logger )
		{
			await StopAsync( logger );
		}

		public void RecoverState ( RecoverTaskRequest request, IisTaskHandleState state )
		{
			_taskConfig = request.Handle.Config;
			_appPoolName = state.AppPoolName;
			_websiteName = state.WebsiteName;
			_startDate = state.StartDate;
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
	}
}