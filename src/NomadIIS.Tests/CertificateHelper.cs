using System;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Security.Cryptography;
using System.Net;

namespace NomadIIS.Tests;

public static class CertificateHelper
{
	public static string GenerateSelfSignedCertificate ( string commonName, string targetFilePath, string? password )
	{
		var certificateName = $"CN={commonName}";

		using var rsa = RSA.Create( 2048 );

		var certRequest = new CertificateRequest( certificateName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1 );

		// Specify basic constraints (self-signed, no CA)
		certRequest.CertificateExtensions.Add( new X509BasicConstraintsExtension( false, false, 0, false ) );

		var serverAuthOid = new Oid( "1.3.6.1.5.5.7.3.1", "Server Authentication" );
		var clientAuthOid = new Oid( "1.3.6.1.5.5.7.3.2", "Client Authentication" );

		// Create an OidCollection and add the OIDs
		OidCollection oidCollection = [serverAuthOid,  clientAuthOid];

		// Create the X509EnhancedKeyUsageExtension with the OidCollection
		X509EnhancedKeyUsageExtension ekuExtension = new X509EnhancedKeyUsageExtension( oidCollection, false );

		// Get the ASN.1 encoded data for the extension
		AsnEncodedData asnData = new AsnEncodedData( ekuExtension.Oid, ekuExtension.RawData );

		certRequest.CertificateExtensions.Add( new X509Extension( asnData, false ) );

		certRequest.CertificateExtensions.Add(
			new X509KeyUsageExtension(
				X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, true ) );

		var sanBuilder = new SubjectAlternativeNameBuilder();
		sanBuilder.AddDnsName( "localhost" );
		sanBuilder.AddDnsName( "127.0.0.1" );
		sanBuilder.AddIpAddress( IPAddress.Loopback );

		certRequest.CertificateExtensions.Add( sanBuilder.Build() );

		// Generate the certificate
		var certificate = certRequest.CreateSelfSigned(
			DateTimeOffset.Now, DateTimeOffset.Now.AddDays( 2 ) );

		// Export to PFX (PKCS #12)
		var pfxBytes = certificate.Export( X509ContentType.Pfx, password );

		File.WriteAllBytes( targetFilePath, pfxBytes );

		return certificate.Thumbprint;
	}
}
