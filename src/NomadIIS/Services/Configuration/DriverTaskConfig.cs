﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NomadIIS.Services.Configuration;

public sealed class DriverTaskConfig
{
	[ConfigurationField( "target_website" )]
	public string? TargetWebsite { get; set; }

	[ConfigurationCollectionField( "applications", "application", 1 )]
	public DriverTaskConfigApplication[] Applications { get; set; } = default!;

	[ConfigurationField( "managed_pipeline_mode" )]
	public Microsoft.Web.Administration.ManagedPipelineMode? ManagedPipelineMode { get; set; }

	[ConfigurationField( "managed_runtime_version" )]
	public string? ManagedRuntimeVersion { get; set; }

	[ConfigurationField( "start_mode" )]
	public Microsoft.Web.Administration.StartMode? StartMode { get; set; }

	[ConfigurationField( "idle_timeout" )]
	public TimeSpan? IdleTimeout { get; set; }

	[ConfigurationField( "disable_overlapped_recycle" )]
	public bool DisabledOverlappedRecycle { get; set; }

	[ConfigurationField( "periodic_restart" )]
	public TimeSpan? PeriodicRestart { get; set; }

	[ConfigurationField( "enable_udp_logging" )]
	public bool EnableUdpLogging { get; set; }

	[DefaultValue( true )]
	[ConfigurationField( "permit_iusr" )]
	public bool PermitIusr { get; set; } = true;

	[ConfigurationCollectionField( "bindings", "binding", 0, 2 )]
	public DriverTaskConfigBinding[] Bindings { get; set; } = default!;
}

public sealed class DriverTaskConfigApplication
{
	[ConfigurationField( "alias" )]
	public string? Alias { get; set; }

	[Required]
	[ConfigurationField( "path" )]
	public string Path { get; set; } = default!;

	[ConfigurationField( "enable_preload" )]
	public bool? EnablePreload { get; set; }

	[ConfigurationCollectionField( "virtual_directories", "virtual_directory" )]
	public DriverTaskConfigVirtualDirectory[]? VirtualDirectories { get; set; }
}

public sealed class DriverTaskConfigVirtualDirectory
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

	[ConfigurationField( "file" )]
	public string? File { get; set; }

	[ConfigurationField( "password" )]
	public string? Password { get; set; }

	[ConfigurationField( "use_self_signed" )]
	public bool UseSelfSigned { get; set; }
}

public enum DriverTaskConfigBindingType
{
	Http,
	Https
}
