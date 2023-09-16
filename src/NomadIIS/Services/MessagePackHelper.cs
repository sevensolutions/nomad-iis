using Google.Protobuf;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NomadIIS.Services;

internal static class MessagePackHelper
{
	public static DriverTaskConfig DecodeAsTaskConfig ( this ByteString byteString )
	{
		if ( byteString is null )
			throw new ArgumentNullException( nameof( byteString ) );

		var config = MessagePackSerializer.Deserialize<Dictionary<object, object>>( byteString.Memory );

		DriverTaskConfigApplication[]? applications = null;

		if ( config.TryGetValue( "applications", out var rawApplications ) && rawApplications is object[] objApplications )
		{
			applications = objApplications.Select( x =>
			{
				if ( x is not Dictionary<object, object> application )
					throw new NotSupportedException( "Invalid application object." );

				string? alias = null;
				if ( application.TryGetValue( "alias", out var rawAlias ) && rawAlias is string vAlias )
					alias = vAlias;

				if ( !application.TryGetValue( "path", out var rawPath ) || rawPath is not string path || string.IsNullOrWhiteSpace( path ) )
					throw new KeyNotFoundException( "Missing required value path in application block." );

				bool? enablePreload = null;
				if ( application.TryGetValue( "enable_preload", out var rawEnablePreload ) && rawEnablePreload is bool vEnablePreload )
					enablePreload = vEnablePreload;

				return new DriverTaskConfigApplication
				{
					Alias = alias,
					Path = path,
					EnablePreload = enablePreload
				};
			} ).ToArray();
		}

		if ( applications is null || applications.Length < 1 )
			throw new NotSupportedException( "There must be one or more applications specified." );

		if ( applications.Select( x => x.Alias ).Distinct().Count() != applications.Length )
			throw new NotSupportedException( "Every application alias must be unique." );

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

		if ( config.TryGetValue( "idle_timeout", out var rawIdleTimeout ) && rawIdleTimeout is string strIdleTimeout && TimeSpanHelper.TryParse( strIdleTimeout, out var timeout ) )
			idleTimeout = timeout;

		if ( !config.TryGetValue( "disable_overlapped_recycle", out var rawDisableOverlappedRecycle ) || rawDisableOverlappedRecycle is not bool disabledOverlappedRecycle )
			disabledOverlappedRecycle = false;

		TimeSpan? periodicRestart = null;

		if ( config.TryGetValue( "periodic_restart", out var rawPeriodicRestart ) && rawPeriodicRestart is string strPeriodicRestart && TimeSpanHelper.TryParse( strPeriodicRestart, out var timeout2 ) )
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

				string? hostname = null;
				if ( binding.TryGetValue( "hostname", out var rawHostname ) && rawHostname is string vHostname )
					hostname = vHostname;

				bool? requireSni = null;
				if ( binding.TryGetValue( "require_sni", out var rawRequireSni ) && rawRequireSni is bool vRequireSni )
					requireSni = vRequireSni;

				string? ipAddress = null;
				if ( binding.TryGetValue( "ip_address", out var rawIpAddress ) && rawIpAddress is string vIpAddress )
					ipAddress = vIpAddress;

				string? certificateHash = null;
				if ( binding.TryGetValue( "certificate_hash", out var rawCertificateHash ) && rawCertificateHash is string vCertificateHash )
					certificateHash = vCertificateHash;

				return new DriverTaskConfigBinding
				{
					Type = type,
					PortLabel = port,
					Hostname = hostname,
					RequireSni = requireSni,
					IPAddress = ipAddress,
					CertificateHash = certificateHash
				};
			} ).ToArray();
		}

		if ( bindings is null || bindings.Length < 1 || bindings.Length > 2 )
			throw new NotSupportedException( "There must be exactly one or two bindings specified." );

		return new DriverTaskConfig()
		{
			Applications = applications,
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
	public required DriverTaskConfigApplication[] Applications { get; init; }
	public required string ManagedPipelineMode { get; init; }
	public required string? ManagedRuntimeVersion { get; init; }
	public required string StartMode { get; init; }
	public required TimeSpan? IdleTimeout { get; init; }
	public required bool DisabledOverlappedRecycle { get; init; }
	public required TimeSpan? PeriodicRestart { get; init; }
	public required DriverTaskConfigBinding[] Bindings { get; init; }
}

public sealed class DriverTaskConfigApplication
{
	public string? Alias { get; set; }
	public required string Path { get; init; }
	public bool? EnablePreload { get; init; }
}

public sealed class DriverTaskConfigBinding
{
	public required string Type { get; init; }
	public required string PortLabel { get; init; }
	public string? Hostname { get; init; }
	public bool? RequireSni { get; init; }
	public string? IPAddress { get; init; }
	public string? CertificateHash { get; init; }
}
