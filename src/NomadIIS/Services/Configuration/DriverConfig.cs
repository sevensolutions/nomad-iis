using System;
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

	[DefaultValue( "C:\\inetpub\\wwwroot" )]
	[ConfigurationField( "placeholder_app_path" )]
	public string? PlaceholderAppPath { get; set; }

	[ConfigurationCollectionField( "procdumps", "procdump", 0, 1 )]
	public DriverConfigProcdump[] Procdumps { get; set; } = default!;

	[ConfigurationField( "allowed_apppool_identities" )]
	public string[]? AllowedAppPoolIdentities { get; set; } = ["ApplicationPoolIdentity"];

	[ConfigurationField( "allowed_apppool_users" )]
	public string[]? AllowedAppPoolUsers { get; set; }

	[ConfigurationCollectionField( "applicationPools", "applicationPool", 0 )]
	public DriverConfigApplicationPool[] ApplicationPools { get; set; } = default!;
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

public sealed class DriverConfigApplicationPool
{
	[ConfigurationField( "identity" )]
	[DefaultValue( "ApplicationPoolIdentity" )]
	public string Identity { get; set; } = "ApplicationPoolIdentity";

	[ConfigurationField( "username" )]
	public string? Username { get; set; }

	[ConfigurationField( "password" )]
	public string? Password { get; set; }
}
