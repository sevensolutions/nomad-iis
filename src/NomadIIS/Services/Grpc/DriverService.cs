using Google.Protobuf;
using Grpc.Core;
using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using Microsoft.Extensions.Logging;
using NomadIIS.Services.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace NomadIIS.Services.Grpc;

public sealed class DriverService : Driver.DriverBase
{
	private readonly ILogger<DriverService> _logger;
	private readonly ManagementService _managementService;

	public DriverService ( ILogger<DriverService> logger, ManagementService managementService )
	{
		_logger = logger;
		_managementService = managementService;
	}

	public override Task<CapabilitiesResponse> Capabilities ( CapabilitiesRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( Capabilities ) );

		return Task.FromResult( new CapabilitiesResponse()
		{
			Capabilities = new DriverCapabilities()
			{
				SendSignals = true,
				Exec = false,
				FsIsolation = DriverCapabilities.Types.FSIsolation.None
			}
		} );
	}

	public override Task<TaskConfigSchemaResponse> TaskConfigSchema ( TaskConfigSchemaRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( TaskConfigSchema ) );

		return Task.FromResult( new TaskConfigSchemaResponse()
		{
			Spec = HclSpecGenerator.Generate<DriverTaskConfig>()
		} );
	}

	public override async Task Fingerprint ( FingerprintRequest request, IServerStreamWriter<FingerprintResponse> responseStream, ServerCallContext context )
	{
		_logger.LogDebug( nameof( Fingerprint ) );

		var iisVersion = "Unknown";

		var inetMgr = Path.Combine( Environment.SystemDirectory, @"inetsrv\InetMgr.exe" );
		if ( File.Exists( inetMgr ) )
			iisVersion = FileVersionInfo.GetVersionInfo( inetMgr ).ProductVersion;

		try
		{
			while ( !context.CancellationToken.IsCancellationRequested )
			{
				FingerprintResponse.Types.HealthState status = FingerprintResponse.Types.HealthState.Undetected;
				string healthDescription = "";

				if ( _managementService.DriverEnabled )
				{
					try
					{
						using var sc = new ServiceController( "w3svc" );

						switch ( sc.Status )
						{
							case ServiceControllerStatus.Running:
								status = FingerprintResponse.Types.HealthState.Healthy;
								healthDescription = "Healthy";
								break;

							default:
								status = FingerprintResponse.Types.HealthState.Unhealthy;
								healthDescription = "IIS (w3svc) is not running.";
								break;
						}
					}
					catch ( Exception )
					{
						status = FingerprintResponse.Types.HealthState.Undetected;
						healthDescription = "An error occurred while detecting the state of w3svc.";
					}
				}
				else
				{
					status = FingerprintResponse.Types.HealthState.Undetected;
					healthDescription = "Driver disabled";
				}				

				await responseStream.WriteAsync( new FingerprintResponse()
				{
					Attributes =
					{
						{ $"driver.{PluginInfo.Name}.version", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ StringVal = PluginInfo.Version } },
						{ $"driver.{PluginInfo.Name}.iis_version", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ StringVal = iisVersion } }
					},
					Health = status,
					HealthDescription = healthDescription
				} );

				await Task.Delay( _managementService.FingerprintInterval, context.CancellationToken );
			}
		}
		catch ( OperationCanceledException )
		{
		}
	}

	public override async Task<StartTaskResponse> StartTask ( StartTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( StartTask ) );

		var task = request.Task;

		var handle = _managementService.CreateHandle( task.Id );
		
		try
		{
			await handle.RunAsync( _logger, task );

			return new StartTaskResponse()
			{
				Handle = new TaskHandle()
				{
					State = TaskState.Running,
					Config = task,
					Version = 1 // Driver State Version
				},
				Result = StartTaskResponse.Types.Result.Success
			};
		}
		catch ( Exception ex )
		{
			_logger.LogError( ex, $"Failed to start task {task.Id}." );

			handle.Dispose();

			return new StartTaskResponse()
			{
				Handle = new TaskHandle()
				{
					State = TaskState.Exited,
					Config = task,
					Version = 1
				},
				DriverErrorMsg = ex.Message,
				Result = StartTaskResponse.Types.Result.Fatal
			};
		}
	}

	public override async Task<StopTaskResponse> StopTask ( StopTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( StopTask ) );

		var handle = _managementService.TryGetHandle( request.TaskId );

		if ( handle is not null )
		{
			await handle.StopAsync( _logger );

			handle.Dispose();
		}

		return new StopTaskResponse();
	}

	public override async Task<SignalTaskResponse> SignalTask ( SignalTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( SignalTask ) );

		var handle = _managementService.TryGetHandle( request.TaskId );

		if ( handle is not null )
			await handle.SignalAsync( _logger, request.Signal );

		return new SignalTaskResponse();
	}

	public override async Task<DestroyTaskResponse> DestroyTask ( DestroyTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( DestroyTask ) );

		var handle = _managementService.TryGetHandle( request.TaskId );

		if ( handle is not null )
		{
			await handle.DestroyAsync( _logger );

			handle.Dispose();
		}

		return new DestroyTaskResponse();
	}

	public override async Task TaskEvents ( TaskEventsRequest request, IServerStreamWriter<DriverTaskEvent> responseStream, ServerCallContext context )
	{
		_logger.LogDebug( nameof( TaskEvents ) );

		try
		{
			await foreach ( var ev in _managementService.ReadAllEventsAsync( context.CancellationToken ) )
				await responseStream.WriteAsync( ev );
		}
		catch ( OperationCanceledException )
		{
		}
	}

	public override async Task<InspectTaskResponse> InspectTask ( InspectTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( InspectTask ) );

		var handle = _managementService.GetHandle( request.TaskId );

		return await handle.InspectAsync();
	}

	public override Task<CreateNetworkResponse> CreateNetwork ( CreateNetworkRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( CreateNetwork ) );

		return base.CreateNetwork( request, context );
	}

	public override Task<DestroyNetworkResponse> DestroyNetwork ( DestroyNetworkRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( DestroyNetwork ) );

		return base.DestroyNetwork( request, context );
	}

	public override Task<ExecTaskResponse> ExecTask ( ExecTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( ExecTask ) );

		return base.ExecTask( request, context );
	}

	public override Task ExecTaskStreaming ( IAsyncStreamReader<ExecTaskStreamingRequest> requestStream, IServerStreamWriter<ExecTaskStreamingResponse> responseStream, ServerCallContext context )
	{
		return base.ExecTaskStreaming( requestStream, responseStream, context );
	}

	public override Task<RecoverTaskResponse> RecoverTask ( RecoverTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( RecoverTask ) );

		var handle = _managementService.CreateHandle( request.TaskId );

		if ( handle is not null )
			handle.RecoverState( request );

		return Task.FromResult( new RecoverTaskResponse() );
	}

	public override async Task TaskStats ( TaskStatsRequest request, IServerStreamWriter<TaskStatsResponse> responseStream, ServerCallContext context )
	{
		_logger.LogDebug( nameof( TaskStats ) );

		try
		{
			var interval = request.CollectionInterval.ToTimeSpan();

			while ( !context.CancellationToken.IsCancellationRequested )
			{
				var handle = _managementService.TryGetHandle( request.TaskId );

				if ( handle is null )
					break;

				var statistics = await handle.GetStatisticsAsync( _logger );

				await responseStream.WriteAsync( new TaskStatsResponse()
				{
					Stats = new TaskStats()
					{
						Id = request.TaskId,
						AggResourceUsage = statistics,
						Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset( DateTimeOffset.Now )
					}
				} );

				await Task.Delay( interval, context.CancellationToken );
			}
		}
		catch ( Exception )
		{
		}
	}

	public override async Task<WaitTaskResponse> WaitTask ( WaitTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( WaitTask ) );

		var handle = _managementService.TryGetHandle( request.TaskId );

		var exitCode = handle is not null ? await handle.WaitAsync( _logger ) : 0;

		return new WaitTaskResponse()
		{
			Result = new ExitResult()
			{
				ExitCode = exitCode
			}
		};
	}
}
