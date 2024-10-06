using System.Security.Cryptography.X509Certificates;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace NomadIIS.Services;

internal static class CertificateHelper
{
	private const string NomadCertificateFriendlyNamePrefix = "[MBN]"; // MBN = Managed by Nomad

	private static readonly Regex _safeThumbprintRegex = new Regex( @"[^\da-fA-F]" );

	private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim( 1, 1 );

	public static async Task<(X509Certificate2 Certificate, string? StoreName)?> InstallPfxCertificateAsync ( string filePath, string? password )
	{
		if ( string.IsNullOrEmpty( filePath ) )
			throw new ArgumentException( nameof( filePath ) );

		return await InstallPfxCertificateAsync( await File.ReadAllBytesAsync( filePath ), password );
	}
	private static async Task<(X509Certificate2 Certificate, string? StoreName)?> InstallPfxCertificateAsync ( byte[] pfxBytes, string? password )
	{
		if ( pfxBytes is null )
			throw new ArgumentException( nameof( pfxBytes ) );

		var certificate = LoadFromPfxFile( pfxBytes, password );

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
				if ( !string.IsNullOrEmpty( certificate.FriendlyName ) )
					certificate.FriendlyName = $"{NomadCertificateFriendlyNamePrefix} {certificate.FriendlyName}";
				else
					certificate.FriendlyName = $"{NomadCertificateFriendlyNamePrefix} {GetCommonName( certificate ) ?? "Unknown"}";

				store.Add( certificate );
			}
		}
		finally
		{
			_semaphore.Release();
		}

		return await FindCertificateByThumbprintAsync( thumbprint );
	}
	public static async Task<(X509Certificate2 Certificate, string? StoreName)?> InstallPemCertificateAsync ( string certificateFilePath, string keyFilePath )
	{
		if ( string.IsNullOrEmpty( certificateFilePath ) )
			throw new ArgumentException( nameof( certificateFilePath ) );
		if ( string.IsNullOrEmpty( keyFilePath ) )
			throw new ArgumentException( nameof( keyFilePath ) );

		var certificate = LoadFromPemFiles( certificateFilePath, keyFilePath );

		// Note: Directly importing the certificate read from pem doesn't work,
		// because we also need to import the Private Key.
		var pfxBytes = certificate.Export( X509ContentType.Pfx );

		return await InstallPfxCertificateAsync( pfxBytes, null );
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
					.Where( x => x.FriendlyName is not null && x.FriendlyName.StartsWith( NomadCertificateFriendlyNamePrefix ) );

				foreach ( var certificate in certificates )
					store.Remove( certificate );
			}
		}
		finally
		{
			_semaphore.Release();
		}
	}

	public static X509Certificate2 LoadFromPfxFile ( byte[] pfxBytes, string? password )
	{
		if ( pfxBytes is null )
			throw new ArgumentException( nameof( pfxBytes ) );

		var certificate = new X509Certificate2( pfxBytes, password,
			X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable );

		return certificate;
	}
	public static X509Certificate2 LoadFromPemFiles ( string certFilePath, string keyFilePath )
	{
		if ( string.IsNullOrEmpty( certFilePath ) )
			throw new ArgumentException( nameof( certFilePath ) );
		if ( string.IsNullOrEmpty( keyFilePath ) )
			throw new ArgumentException( nameof( keyFilePath ) );

		var certPem = File.ReadAllText( certFilePath );
		var certificate = LoadCertificateFromPem( certPem );

		var keyPem = File.ReadAllText( keyFilePath );
		var privateKey = LoadPrivateKeyFromPem( keyPem );

		// Combine the certificate and the private key into X509Certificate2
		certificate = certificate.CopyWithPrivateKey( privateKey );

		return certificate;
	}
	private static X509Certificate2 LoadCertificateFromPem ( string pemContent )
	{
		var certBase64 = ExtractBase64Content( pemContent, "CERTIFICATE" );
		var certBytes = Convert.FromBase64String( certBase64 );

		return new X509Certificate2( certBytes, (string?)null,
			X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable );
	}
	private static RSA LoadPrivateKeyFromPem ( string pemContent )
	{
		var keyBase64 = ExtractBase64Content( pemContent, "PRIVATE KEY" );
		var keyBytes = Convert.FromBase64String( keyBase64 );

		var rsa = RSA.Create();

		rsa.ImportPkcs8PrivateKey( keyBytes, out _ );

		return rsa;
	}
	private static string ExtractBase64Content ( string pemContent, string section )
	{
		StringBuilder result = new StringBuilder();
		using var reader = new StringReader( pemContent );

		bool isReadingContent = false;
		string? line;

		while ( ( line = reader.ReadLine() ) != null )
		{
			if ( line.Contains( $"BEGIN {section}" ) )
				isReadingContent = true;
			else if ( line.Contains( $"END {section}" ) )
				isReadingContent = false;
			else if ( isReadingContent )
				result.Append( line );
		}

		return result.ToString();
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

	private static string? GetCommonName ( X509Certificate2 certificate )
	{
		if ( certificate.Subject is null )
			return null;

		var fields = certificate.Subject.Split( ',', StringSplitOptions.RemoveEmptyEntries );

		foreach ( var field in fields )
		{
			if ( field.Trim().StartsWith( "CN=", StringComparison.OrdinalIgnoreCase ) )
				return field[3..].Trim();
		}

		return null;
	}
}
