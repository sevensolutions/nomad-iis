using Microsoft.Web.Administration;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NomadIIS.Services.Configuration;

public sealed class DriverTaskConfig
{
	[ConfigurationField( "target_website" )]
	public string? TargetWebsite { get; set; }

	[ConfigurationCollectionField( "applicationPools", "applicationPool", 0 )]
	public DriverTaskConfigApplicationPool[] ApplicationPoolsOld { get; set; } = default!;

	[ConfigurationCollectionField( "application_pools", "application_pool", 0 )]
	public DriverTaskConfigApplicationPool[] ApplicationPoolsNew { get; set; } = default!;

	public DriverTaskConfigApplicationPool[] ApplicationPools
	{
		get => ( ApplicationPoolsOld ?? [] ).Concat( ApplicationPoolsNew ?? [] ).ToArray();
		set
		{
			ApplicationPoolsOld = [];
			ApplicationPoolsNew = value;
		}
	}

	[ConfigurationCollectionField( "applications", "application", 1 )]
	public DriverTaskConfigApplication[] Applications { get; set; } = default!;

	[DefaultValue( true )]
	[ConfigurationField( "permit_iusr" )]
	public bool PermitIusr { get; set; } = true;

	[ConfigurationCollectionField( "bindings", "binding", 0, 64 )]
	public DriverTaskConfigBinding[] Bindings { get; set; } = default!;

	[ConfigurationCollectionField( "service_auto_start_providers", "service_auto_start_provider" )]
	public DriverTaskConfigServiceAutoStartProvider[]? ServiceAutoStartProviders { get; set; }

	[ConfigurationCollectionField( "logging", "logging", 0, 1 )]
	public DriverTaskConfigLogging[] Loggings { get; set; } = default!;

	public DriverTaskConfigLogging? Logging
		=> Loggings is not null && Loggings.Length == 1 ? Loggings[0] : null;
}

public sealed class DriverTaskConfigLogging
{
	[DefaultValue( true )]
	[ConfigurationField( "enabled" )]
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// The log file to tail, relative to the task directory. May contain a wildcard, which is
	/// required for appenders that put a timestamp or process id in the file name.
	/// </summary>
	[ConfigurationField( "file" )]
	public string? File { get; set; }

	/// <summary>
	/// Enables ASP.NET Core Module stdout logging for every application of this task and tails
	/// the resulting files. This captures the real stdout of the .NET process without the
	/// application having to be configured for it.
	/// </summary>
	[DefaultValue( false )]
	[ConfigurationField( "aspnetcore_stdout" )]
	public bool AspNetCoreStdout { get; set; }

	[DefaultValue( "1s" )]
	[ConfigurationField( "poll_interval" )]
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds( 1 );

	/// <summary>
	/// Upper bound on how much log data is forwarded to nomad per second.
	/// </summary>
	[DefaultValue( 1048576 )]
	[ConfigurationField( "max_bytes_per_second" )]
	public long MaxBytesPerSecond { get; set; } = 1048576;
}

public sealed class DriverTaskConfigApplicationPool : DriverTaskConfigExtendable
{
	[ConfigurationField( "name" )]
	[DefaultValue( IisTaskHandle.DefaultAppPoolName )]
	public string Name { get; set; } = IisTaskHandle.DefaultAppPoolName;

	[ConfigurationField( "managed_pipeline_mode" )]
	public ManagedPipelineMode? ManagedPipelineMode { get; set; }

	[ConfigurationField( "managed_runtime_version" )]
	public string? ManagedRuntimeVersion { get; set; }

	[ConfigurationField( "start_mode" )]
	public StartMode? StartMode { get; set; }

	[ConfigurationField( "idle_timeout" )]
	public TimeSpan? IdleTimeout { get; set; }

	[ConfigurationField( "disable_overlapped_recycle" )]
	public bool? DisabledOverlappedRecycle { get; set; }

	[ConfigurationField( "periodic_restart" )]
	public TimeSpan? PeriodicRestart { get; set; }

	[ConfigurationField( "enable_32bit_app_on_win64" )]
	public bool? Enable32BitAppOnWin64 { get; set; }

	[ConfigurationField( "service_unavailable_response" )]
	public LoadBalancerCapabilities? ServiceUnavailableResponse { get; set; }

	[ConfigurationField( "queue_length" )]
	public long? QueueLength { get; set; }

	[ConfigurationField( "start_time_limit" )]
	public TimeSpan? StartTimeLimit { get; set; }

	[ConfigurationField( "shutdown_time_limit" )]
	public TimeSpan? ShutdownTimeLimit { get; set; }

	[ConfigurationField( "identity" )]
	[DefaultValue( "ApplicationPoolIdentity" )]
	public string Identity { get; set; } = "ApplicationPoolIdentity";

	[ConfigurationField( "username" )]
	public string? Username { get; set; }

	[ConfigurationField( "password" )]
	public string? Password { get; set; }
}

public sealed class DriverTaskConfigApplication : DriverTaskConfigExtendable
{
	[ConfigurationField( "application_pool" )]
	[DefaultValue( IisTaskHandle.DefaultAppPoolName )]
	public string ApplicationPool { get; set; } = IisTaskHandle.DefaultAppPoolName;

	[ConfigurationField( "alias" )]
	public string? Alias { get; set; }

	[Required]
	[ConfigurationField( "path" )]
	public string Path { get; set; } = default!;

	[ConfigurationField( "enable_preload" )]
	public bool? EnablePreload { get; set; }

	[ConfigurationField( "service_auto_start_enabled" )]
	public bool? ServiceAutoStartEnabled { get; set; }

	[ConfigurationField( "service_auto_start_provider" )]
	public string? ServiceAutoStartProvider { get; set; }

	[ConfigurationCollectionField( "virtual_directories", "virtual_directory" )]
	public DriverTaskConfigVirtualDirectory[]? VirtualDirectories { get; set; }
}

public sealed class DriverTaskConfigVirtualDirectory : DriverTaskConfigExtendable
{
	[Required]
	[ConfigurationField( "alias" )]
	public string Alias { get; set; } = default!;

	[Required]
	[ConfigurationField( "path" )]
	public string Path { get; set; } = default!;
}

public sealed class DriverTaskConfigBinding
{
	[Required]
	[ConfigurationField( "type" )]
	public DriverTaskConfigBindingType Type { get; set; } = default!;

	[Required]
	[ConfigurationField( "port" )]
	public string Port { get; set; } = default!;

	[ConfigurationField( "hostname" )]
	public string? Hostname { get; set; }

	[ConfigurationField( "require_sni" )]
	public bool? RequireSni { get; set; }

	[ConfigurationField( "ip_address" )]
	public string? IPAddress { get; set; }

	[ConfigurationCollectionField( "certificates", "certificate", 0, 1 )]
	public DriverTaskConfigCertificate[] Certificates { get; set; } = default!;
}

public sealed class DriverTaskConfigCertificate
{
	[ConfigurationField( "thumbprint" )]
	public string? Thumbprint { get; set; }

	[ConfigurationField( "pfx_file" )]
	public string? PfxFile { get; set; }
	[ConfigurationField( "password" )]
	public string? Password { get; set; }

	[ConfigurationField( "cert_file" )]
	public string? CertFile { get; set; }
	[ConfigurationField( "key_file" )]
	public string? KeyFile { get; set; }

	[ConfigurationField( "use_self_signed" )]
	public bool UseSelfSigned { get; set; }
}

public enum DriverTaskConfigBindingType
{
	Http,
	Https
}

public sealed class DriverTaskConfigExtension
{
	[Required]
	[ConfigurationField( "name" )]
	public string Name { get; set; } = default!;

	[Required]
	[ConfigurationField( "value" )]
	public string Value { get; set; } = default!;
}

public abstract class DriverTaskConfigExtendable
{
	[ConfigurationCollectionField( "extensions", "extension" )]
	public DriverTaskConfigExtension[]? Extensions { get; set; }
}

public sealed class DriverTaskConfigServiceAutoStartProvider
{
	[Required]
	[ConfigurationField( "name" )]
	public string Name { get; set; } = default!;

	[Required]
	[ConfigurationField( "type" )]
	public string Type { get; set; } = default!;
}
