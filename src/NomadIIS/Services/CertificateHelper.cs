using System.Security.Cryptography.X509Certificates;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Net;
using System.Security.Cryptography;

namespace NomadIIS.Services;

internal static class CertificateHelper
{
	private const string NomadCertificateFriendlyName = "Installed by Nomad IIS";

	private static readonly Regex _safeThumbprintRegex = new Regex( @"[^\da-fA-F]" );

	private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim( 1, 1 );

	public static async Task<(X509Certificate2 Certificate, string? StoreName)?> InstallCertificateAsync ( string filePath, string? password )
	{
		if ( string.IsNullOrEmpty( filePath ) )
			throw new ArgumentException( nameof( filePath ) );

		var certificate = new X509Certificate2( filePath, password,
			X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable );

		await _semaphore.WaitAsync();

		string thumbprint;

		try
		{
			using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

			store.Open( OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite );

			thumbprint = MakeSafeThumbprint( certificate.Thumbprint );

			var found = store.Certificates
				.Find( X509FindType.FindByThumbprint, thumbprint, false )
				.FirstOrDefault();

			if ( found is null )
			{
				certificate.FriendlyName = NomadCertificateFriendlyName;

				store.Add( certificate );
			}
		}
		finally
		{
			_semaphore.Release();
		}

		return await FindCertificateByThumbprintAsync( thumbprint );
	}
	public static async Task UninstallCertificatesAsync ( HashSet<string> thumbprints )
	{
		if ( thumbprints is null )
			throw new ArgumentNullException( nameof( thumbprints ) );

		await _semaphore.WaitAsync();

		try
		{
			using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

			store.Open( OpenFlags.ReadWrite );

			foreach ( var thumbprint in thumbprints )
			{
				// Make sure we only uninstall the ones we installed by ourself.
				var certificates = store.Certificates
					.Find( X509FindType.FindByThumbprint, MakeSafeThumbprint( thumbprint ), false )
					.Where( x => x.FriendlyName == NomadCertificateFriendlyName );

				foreach ( var certificate in certificates )
					store.Remove( certificate );
			}
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public static async Task<(X509Certificate2 Certificate, string? StoreName)?> FindCertificateByThumbprintAsync ( string thumbprint )
	{
		if ( string.IsNullOrEmpty( thumbprint ) )
			throw new ArgumentNullException( nameof( thumbprint ) );

		thumbprint = MakeSafeThumbprint( thumbprint );

		await _semaphore.WaitAsync();

		try
		{
			using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

			store.Open( OpenFlags.ReadOnly );

			var certificate = store.Certificates
				.Find( X509FindType.FindByThumbprint, thumbprint, false )
				.FirstOrDefault();

			if ( certificate is not null )
				return (certificate, store.Name);

			return null;
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public static string GenerateSelfSignedCertificate ( string commonName, TimeSpan lifetime, string targetFilePath, string? password )
	{
		var certificateName = $"CN={commonName}";

		using var rsa = RSA.Create( 2048 );

		var certRequest = new CertificateRequest( certificateName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1 );

		// Specify basic constraints (self-signed, no CA)
		certRequest.CertificateExtensions.Add( new X509BasicConstraintsExtension( false, false, 0, false ) );

		var serverAuthOid = new Oid( "1.3.6.1.5.5.7.3.1", "Server Authentication" );
		var clientAuthOid = new Oid( "1.3.6.1.5.5.7.3.2", "Client Authentication" );

		// Create an OidCollection and add the OIDs
		OidCollection oidCollection = [serverAuthOid, clientAuthOid];

		// Create the X509EnhancedKeyUsageExtension with the OidCollection
		var ekuExtension = new X509EnhancedKeyUsageExtension( oidCollection, false );

		// Get the ASN.1 encoded data for the extension
		var asnData = new AsnEncodedData( ekuExtension.Oid, ekuExtension.RawData );

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
		var now = DateTimeOffset.Now;
		var certificate = certRequest.CreateSelfSigned( now, now.Add( lifetime ) );

		// Export to PFX (PKCS #12)
		var pfxBytes = certificate.Export( X509ContentType.Pfx, password );

		File.WriteAllBytes( targetFilePath, pfxBytes );

		return certificate.Thumbprint;
	}

	private static string MakeSafeThumbprint ( string thumbprint )
	{
		// Strip any non-hexadecimal values and make uppercase
		return _safeThumbprintRegex.Replace( thumbprint, string.Empty ).ToUpper();
	}
}
