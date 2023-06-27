using Google.Protobuf;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NomadIIS.Services;

internal static class MessagePackHelper
{
	public static DriverTaskConfig DecodeAsTaskConfig ( this ByteString byteString )
	{
		if ( byteString is null )
			throw new ArgumentNullException( nameof( byteString ) );

		var config = MessagePackSerializer.Deserialize<Dictionary<object, object>>( byteString.Memory );

		if ( !config.TryGetValue( "path", out var rawPath ) || rawPath is not string path || string.IsNullOrWhiteSpace( path ) )
			throw new KeyNotFoundException( "Missing required value path in task config." );

		if ( !config.TryGetValue( "managed_pipeline_mode", out var rawManagedPipelineMode ) || rawManagedPipelineMode is not string managedPipelineMode )
			managedPipelineMode = "Integrated";

		if ( managedPipelineMode != "Integrated" && managedPipelineMode != "Classic" )
			throw new ArgumentException( $"Invalid managed_pipeline_mode {managedPipelineMode}. Use either Integrated or Classic." );

		string? managedRuntimeVersion = null;

		if ( config.TryGetValue( "managed_runtime_version", out var rawManagedRuntimeVersion ) && rawManagedRuntimeVersion is string strManagedRuntimeVersion )
			managedRuntimeVersion = strManagedRuntimeVersion;

		if ( !config.TryGetValue( "start_mode", out var rawStartMode ) || rawStartMode is not string startMode )
			startMode = "OnDemand";

		if ( startMode != "OnDemand" && startMode != "AlwaysRunning" )
			throw new ArgumentException( $"Invalid start_mode {startMode}. Use either OnDemand or AlwaysRunning." );

		TimeSpan? idleTimeout = null;

		if ( config.TryGetValue( "idle_timeout", out var rawIdleTimeout ) && rawIdleTimeout is string strIdleTimeout && TimeSpan.TryParse( strIdleTimeout, out var timeout ) )
			idleTimeout = timeout;

		if ( !config.TryGetValue( "disable_overlapped_recycle", out var rawDisableOverlappedRecycle ) || rawDisableOverlappedRecycle is not bool disabledOverlappedRecycle )
			disabledOverlappedRecycle = false;

		TimeSpan? periodicRestart = null;

		if ( config.TryGetValue( "periodic_restart", out var rawPeriodicRestart ) && rawPeriodicRestart is string strPeriodicRestart && TimeSpan.TryParse( strPeriodicRestart, out var timeout2 ) )
			periodicRestart = timeout2;

		DriverTaskConfigBinding[]? bindings = null;

		if ( config.TryGetValue( "bindings", out var rawBindings ) && rawBindings is object[] objBindings )
		{
			bindings = objBindings.Select( x =>
			{
				if ( x is not Dictionary<object, object> binding )
					throw new NotSupportedException( "Invalid binding object." );

				if ( !binding.TryGetValue( "type", out var rawType ) || rawType is not string type || string.IsNullOrWhiteSpace( type ) )
					throw new KeyNotFoundException( "Missing required value type in binding block." );

				if ( !binding.TryGetValue( "port", out var rawPort ) || rawPort is not string port || string.IsNullOrWhiteSpace( port ) )
					throw new KeyNotFoundException( "Missing required port type in binding block." );

				if ( type != "http" && type != "https" )
					throw new NotSupportedException( "Binding type must be either http or https." );

				return new DriverTaskConfigBinding { Type = type, PortLabel = port };
			} ).ToArray();
		}

		if ( bindings is null || bindings.Length < 1 || bindings.Length > 2 )
			throw new NotSupportedException( "There must be exactly one or two bindings specified." );

		return new DriverTaskConfig()
		{
			Path = path,
			ManagedPipelineMode = managedPipelineMode,
			ManagedRuntimeVersion = managedRuntimeVersion,
			StartMode = startMode,
			IdleTimeout = idleTimeout,
			DisabledOverlappedRecycle = disabledOverlappedRecycle,
			PeriodicRestart = periodicRestart,
			Bindings = bindings
		};
	}
}

public sealed class DriverTaskConfig
{
	public required string Path { get; init; }
	public required string ManagedPipelineMode { get; init; }
	public required string? ManagedRuntimeVersion { get; init; }
	public required string StartMode { get; init; }
	public required TimeSpan? IdleTimeout { get; init; }
	public required bool DisabledOverlappedRecycle { get; init; }
	public required TimeSpan? PeriodicRestart { get; init; }
	public required DriverTaskConfigBinding[] Bindings { get; init; }
}

public sealed class DriverTaskConfigBinding
{
	public required string Type { get; init; }
	public required string PortLabel { get; init; }
}
