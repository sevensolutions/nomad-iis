﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace NomadIIS.Services.Configuration;

public sealed class DriverConfig
{
	[DefaultValue( true )]
	[ConfigurationField( "enabled" )]
	public bool Enabled { get; set; }

	[DefaultValue( null )]
	[ConfigurationField( "data_directory" )]
	public string? DataDirectory { get; set; }

	[DefaultValue( "30s" )]
	[ConfigurationField( "fingerprint_interval" )]
	public TimeSpan FingerprintInterval { get; set; }

	[DefaultValue( true )]
	[ConfigurationField( "directory_security" )]
	public bool DirectorySecurity { get; set; }

	[ConfigurationField( "allowed_target_websites" )]
	public string[]? AllowedTargetWebsites { get; set; }
}
