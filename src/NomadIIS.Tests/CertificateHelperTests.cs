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

		Assert.Contains( "<Version>1.21.0</Version>", projectFile );
		Assert.Contains( "<PackageVersion>1.21.0</PackageVersion>", projectFile );
		Assert.Contains( "<PackageLicenseExpression>MIT</PackageLicenseExpression>", projectFile );
	}
}
