﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NomadIIS.Services.Configuration;

public sealed class DriverConfig
{
	[DefaultValue( true )]
	[ConfigurationField( "enabled" )]
	public bool Enabled { get; set; }

	[DefaultValue( "30s" )]
	[ConfigurationField( "fingerprint_interval" )]
	public TimeSpan FingerprintInterval { get; set; }

	[DefaultValue( true )]
	[ConfigurationField( "directory_security" )]
	public bool DirectorySecurity { get; set; } = true;

	[ConfigurationField( "allowed_target_websites" )]
	public string[]? AllowedTargetWebsites { get; set; }

	[DefaultValue( 0 )]
	[ConfigurationField( "udp_logger_port" )]
	public int? UdpLoggerPort { get; set; } = 0;

	[DefaultValue( "C:\\inetpub\\wwwroot" )]
	[ConfigurationField( "placeholder_app_path" )]
	public string? PlaceholderAppPath { get; set; }

	[ConfigurationCollectionField( "procdumps", "procdump", 0, 1 )]
	public DriverConfigProcdump[] Procdumps { get; set; } = default!;
}

public sealed class DriverConfigProcdump
{
	[DefaultValue( "C:\\procdump.exe" )]
	[ConfigurationField( "binary_path" )]
	public string? BinaryPath { get; set; }

	[DefaultValue( false )]
	[ConfigurationField( "accept_eula" )]
	public bool AcceptEula { get; set; }
}
