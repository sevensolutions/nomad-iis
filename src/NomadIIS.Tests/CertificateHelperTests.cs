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
}
