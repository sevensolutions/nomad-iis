using System;
using System.IO;
using NomadIIS.Services;

namespace NomadIIS.Tests;

public class CertificateHelperTests
{
	[Fact]
	public void LoadCertificateFromPemFiles ()
	{
		var certificate = CertificateHelper.LoadFromPemFiles( "Data\\certificates\\cert1.pem", "Data\\certificates\\cert1.key.pem" );

		Assert.NotNull( certificate );
		Assert.Equal( "CN=localhost", certificate.Subject );
	}

	[Fact]
	public void ProjectVersionAndLicenseAreExplicitlySet ()
	{
		var projectFilePath = Path.Combine( AppContext.BaseDirectory, "..", "..", "..", "..", "NomadIIS", "NomadIIS.csproj" );
		var projectFile = File.ReadAllText( Path.GetFullPath( projectFilePath ) );

		Assert.Contains( "<Version>", projectFile );
		Assert.Contains( "<PackageVersion>", projectFile );
		Assert.Contains( "<PackageLicenseExpression>MIT</PackageLicenseExpression>", projectFile );
		Assert.Contains( "<PackageLicenseExpression>", projectFile );
	}

	[Fact]
	public void PluginVersionUsesProjectAssemblyMetadata ()
	{
		var version = typeof( NomadIIS.PluginInfo ).GetField( "Version", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public )?.GetValue( null ) as string;

		Assert.False( string.IsNullOrWhiteSpace( version ) );
		Assert.DoesNotContain( "0.0.0", version );
	}
}
