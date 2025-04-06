using Google.Protobuf;
using Grpc.Core;
using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NomadIIS.Services.Configuration;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection.Metadata;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NtCoreLib.Win32.Process;
using NtCoreLib;
using System.Linq;
using NtCoreLib.Security.Token;

namespace NomadIIS.Services.Grpc;

public sealed class DriverService : Driver.DriverBase
{
	private readonly ILogger<DriverService> _logger;
	private readonly ManagementService _managementService;
	private readonly IConfiguration _configuration;

	public DriverService ( ILogger<DriverService> logger, ManagementService managementService, IConfiguration configuration )
	{
		_logger = logger;
		_managementService = managementService;
		_configuration = configuration;
	}

	public override Task<CapabilitiesResponse> Capabilities ( CapabilitiesRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( Capabilities ) );

		return Task.FromResult( new CapabilitiesResponse()
		{
			Capabilities = new DriverCapabilities()
			{
				SendSignals = true,
				Exec = true,
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

		// Check for availability of some common modules
		var aspnetCoreAvailable = File.Exists( Environment.ExpandEnvironmentVariables( @"%ProgramFiles%\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll" ) );
		var rewriteModuleAvailable = File.Exists( Environment.ExpandEnvironmentVariables( @"%SystemRoot%\System32\inetsrv\rewrite.dll" ) );

		try
		{
#if MANAGEMENT_API
			var managementApiPort = _configuration.GetValue( "management-api-port", 0 );
#endif

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
						{ $"driver.{PluginInfo.Name}.iis_version", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ StringVal = iisVersion } },
						{ $"driver.{PluginInfo.Name}.iis_aspnet_core_available", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ BoolVal = aspnetCoreAvailable } },
						{ $"driver.{PluginInfo.Name}.iis_rewrite_module_available", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ BoolVal = rewriteModuleAvailable } },
						{ $"driver.{PluginInfo.Name}.directory_security_enabled", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ BoolVal = _managementService.DirectorySecurity } },
						{ $"driver.{PluginInfo.Name}.udp_logging_enabled", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ BoolVal = _managementService.UdpLoggerPort is not null } },
						{ $"driver.{PluginInfo.Name}.target_websites_enabled", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ BoolVal = _managementService.AllowedTargetWebsites.Length > 0 } },
#if MANAGEMENT_API
						{ $"driver.{PluginInfo.Name}.management_api_enabled", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ BoolVal = managementApiPort > 0 } },
						{ $"driver.{PluginInfo.Name}.management_api_port", new Hashicorp.Nomad.Plugins.Shared.Structs.Attribute(){ IntVal = managementApiPort } },
#endif
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
			var driverState = await handle.RunAsync( task );

			return new StartTaskResponse()
			{
				Handle = new TaskHandle()
				{
					State = TaskState.Running,
					Config = task,
					Version = driverState.Version,
					DriverState = ByteString.CopyFrom( MessagePackSerializer.Serialize( driverState ) )
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
			await handle.StopAsync();

			handle.Dispose();
		}

		return new StopTaskResponse();
	}

	public override async Task<SignalTaskResponse> SignalTask ( SignalTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( SignalTask ) );

		var handle = _managementService.TryGetHandle( request.TaskId );

		if ( handle is not null )
			await handle.SignalAsync( request.Signal );

		return new SignalTaskResponse();
	}

	public override async Task<DestroyTaskResponse> DestroyTask ( DestroyTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( DestroyTask ) );

		var handle = _managementService.TryGetHandle( request.TaskId );

		if ( handle is not null )
		{
			await handle.DestroyAsync();

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

#if MANAGEMENT_API
	public override async Task ExecTaskStreaming ( IAsyncStreamReader<ExecTaskStreamingRequest> requestStream, IServerStreamWriter<ExecTaskStreamingResponse> responseStream, ServerCallContext context )
	{
		IisTaskHandle? handle = null;
		ProcessRunHandle? runHandle = null;

		await foreach ( var request in requestStream.ReadAllAsync( context.CancellationToken ) )
		{
			if ( request.Setup?.TaskId is not null )
			{
				handle = _managementService.GetHandle( request.Setup.TaskId );

				var commands = request.Setup.Command.ToImmutableArray();
				var command = commands[0];

				var sb = new StringBuilder();

				try
				{
					runHandle = await CreateProcessWithAppPoolIdentity( handle, command, sb, responseStream.WriteAsync );

					await responseStream.WriteAsync( new ExecTaskStreamingResponse()
					{
						Exited = true,
						Stdout = new ExecTaskStreamingIOOperation()
						{
							Data = ByteString.CopyFromUtf8( sb.ToString() ),
							Close = true
						}
					} );
				}
				catch ( Exception ex )
				{
					await responseStream.WriteAsync( new ExecTaskStreamingResponse()
					{
						Exited = true,
						Stdout = new ExecTaskStreamingIOOperation()
						{
							Data = ByteString.CopyFromUtf8( ex.Message ),
							Close = true
						}
					} );
				}
			}
			else if ( handle is not null && runHandle is not null )
			{
				if ( request.Stdin is not null )
				{
					if ( !request.Stdin.Close )
					{
						if ( request.Stdin.Data is not null )
						{
							var stdinData = request.Stdin.Data.ToArray();

							if ( stdinData.Length == 1 && stdinData[0] == '\r' )
								stdinData = new byte[] { (byte)'\r', (byte)'\n' };

							await runHandle.StdinPipe.WriteAsync( stdinData );
						}
					}
					else
					{
						runHandle.Close();
						runHandle.Dispose();

						// TODOPEI: Exit Code
						await responseStream.WriteAsync( new ExecTaskStreamingResponse()
						{
							Exited = true,
							Stdout = new ExecTaskStreamingIOOperation()
							{
								Close = true
							},
							Stderr = new ExecTaskStreamingIOOperation()
							{
								Close = true
							}
						} );
					}
				}
			}
		}
	}


	private class ProcessRunHandle : IDisposable
	{
		public NtToken? Token { get; set; }
		public Win32Process? Process { get; set; }
		public AnonymousPipeServerStream? StdinPipe { get; set; }
		public AnonymousPipeServerStream? StdoutPipe { get; set; }
		public AnonymousPipeServerStream? StderrPipe { get; set; }
		public Thread? StdoutThread { get; set; }

		public void Close ()
		{
			Process?.Terminate( NtStatus.STATUS_CONTROL_C_EXIT );
		}
		public void Dispose ()
		{
			StdinPipe?.Dispose();
			StdoutPipe?.Dispose();
			StderrPipe?.Dispose();

			Process?.Dispose();
			Token?.Dispose();
		}
	}

	private static async Task<ProcessRunHandle> CreateProcessWithAppPoolIdentity ( IisTaskHandle iisTaskHandle, string cmdline, StringBuilder logger, Func<ExecTaskStreamingResponse, Task> onDataReceived )
	{
		if ( iisTaskHandle.TaskConfig is null )
			throw new InvalidOperationException( "Invalid state." );

		// TODOPEI: At the moment, this code relies on the AppPool being running to steal the auth token.
		// Check if we can use LsaLogonUserExEx() to create our own token.
		var appPoolProcessId = await iisTaskHandle.TryGetAppPoolProcessId();

		var w3wp = NtProcess
			.GetProcesses( ProcessAccessRights.AllAccess )
			.FirstOrDefault( x => x.ProcessId == appPoolProcessId );

		if ( w3wp is null )
			throw new Exception( "w3wp not running" );

		var token = w3wp.OpenToken();

		var stdinPipe = new AnonymousPipeServerStream( PipeDirection.Out, HandleInheritability.Inheritable );
		var stdoutPipe = new AnonymousPipeServerStream( PipeDirection.In, HandleInheritability.Inheritable );
		var stderrPipe = new AnonymousPipeServerStream( PipeDirection.In, HandleInheritability.Inheritable );

		var stdoutThread = new Thread( StdoutListener );
		var stderrThread = new Thread( StderrListener );

		stdoutThread.Start();
		stderrThread.Start();

		async void StdoutListener ()
		{
			try
			{
				var buffer = new byte[4096];
				int bytesRead;

				while ( ( bytesRead = await stdoutPipe.ReadAsync( buffer, 0, buffer.Length ) ) > 0 )
				{
					await onDataReceived( new ExecTaskStreamingResponse()
					{
						Exited = false,
						Stdout = new ExecTaskStreamingIOOperation()
						{
							Data = ByteString.CopyFrom( new ReadOnlySpan<byte>( buffer, 0, bytesRead ) ),
							Close = false
						}
					} );
				}

				await onDataReceived( new ExecTaskStreamingResponse()
				{
					Exited = false,
					Stdout = new ExecTaskStreamingIOOperation()
					{
						Close = true
					}
				} );
			}
			catch ( Exception ex )
			{
				await onDataReceived( new ExecTaskStreamingResponse()
				{
					Exited = false,
					Stdout = new ExecTaskStreamingIOOperation()
					{
						Data = ByteString.CopyFromUtf8( ex.Message ),
						Close = true
					}
				} );
			}
		}

		async void StderrListener ()
		{
			try
			{
				var buffer = new byte[4096];
				int bytesRead;

				while ( ( bytesRead = await stderrPipe.ReadAsync( buffer, 0, buffer.Length ) ) > 0 )
				{
					await onDataReceived( new ExecTaskStreamingResponse()
					{
						Exited = false,
						Stderr = new ExecTaskStreamingIOOperation()
						{
							Data = ByteString.CopyFrom( new ReadOnlySpan<byte>( buffer, 0, bytesRead ) ),
							Close = false
						}
					} );
				}

				await onDataReceived( new ExecTaskStreamingResponse()
				{
					Exited = false,
					Stdout = new ExecTaskStreamingIOOperation()
					{
						Close = true
					}
				} );
			}
			catch ( Exception ex )
			{
				await onDataReceived( new ExecTaskStreamingResponse()
				{
					Exited = false,
					Stdout = new ExecTaskStreamingIOOperation()
					{
						Data = ByteString.CopyFromUtf8( ex.Message ),
						Close = true
					}
				} );
			}
		}

		var workingDirectory = Path.Combine( iisTaskHandle.TaskConfig.AllocDir, iisTaskHandle.TaskConfig.Name );

		var config = new Win32ProcessConfig()
		{
			Token = token, // TODO
			TokenCall = Win32ProcessConfigTokenCallFlags.Both,
			CurrentDirectory = workingDirectory,
			ApplicationName = "C:\\Windows\\System32\\cmd.exe", // TODO
			CommandLine = "\"C:\\Windows\\System32\\cmd.exe\"",
			StdInputHandle = stdinPipe.ClientSafePipeHandle.DangerousGetHandle(),
			StdOutputHandle = stdoutPipe.ClientSafePipeHandle.DangerousGetHandle(),
			StdErrorHandle = stderrPipe.ClientSafePipeHandle.DangerousGetHandle(),
			InheritHandles = true, // This is important for Stdout/Stderr/Stdin to work
			LogonFlags = CreateProcessLogonFlags.None, // TODOPEI: Do we need this?
			CreationFlags = CreateProcessFlags.NoWindow | CreateProcessFlags.NewConsole
		};

		using ( var process = config.Create() )
		{
			return new ProcessRunHandle()
			{
				Token = token,
				Process = process,
				StdinPipe = stdinPipe,
				StdoutPipe = stdoutPipe,
				StderrPipe = stderrPipe,
				StdoutThread = stdoutThread
			};
		}
	}
#endif

	public override Task<RecoverTaskResponse> RecoverTask ( RecoverTaskRequest request, ServerCallContext context )
	{
		_logger.LogDebug( nameof( RecoverTask ) );

		// Note: Looks like request.TaskId is always empty here.
		var handle = _managementService.CreateHandle( request.Handle.Config.Id );

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

				var statistics = await handle.GetStatisticsAsync();

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

		var exitCode = handle is not null ? await handle.WaitAsync() : 0;

		return new WaitTaskResponse()
		{
			Result = new ExitResult()
			{
				ExitCode = exitCode
			}
		};
	}
}
