using System.Security.Cryptography.X509Certificates;
using System;
using System.Linq;

namespace NomadIIS.Services;

internal static class CertificateHelper
{
	public static (X509Certificate2 Certificate, string? StoreName) InstallCertificate ( string filePath, string friendlyName, out bool installed, string? password )
	{
		if ( string.IsNullOrEmpty( filePath ) )
			throw new ArgumentNullException( nameof( filePath ) );

		using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

		store.Open( OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite );

		var certificate = new X509Certificate2( filePath, password );

		var hash = certificate.GetCertHashString();

		var found = store.Certificates.FirstOrDefault(
			x => x.GetCertHashString().Equals( hash, StringComparison.InvariantCultureIgnoreCase ) );

		installed = false;

		if ( found is null )
		{
			certificate.FriendlyName = friendlyName;

			store.Add( certificate );

			installed = true;
			found = certificate;
		}

		return (found, store.Name);
	}
	public static void UninstallCertificatesByFriendlyName ( string name )
	{
		if ( string.IsNullOrEmpty( name ) )
			throw new ArgumentNullException( nameof( name ) );

		using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

		store.Open( OpenFlags.ReadWrite );

		var certificates = store.Certificates
			.Where( x => x.FriendlyName == name );

		foreach ( var certificate in certificates )
			store.Remove( certificate );
	}

	public static (X509Certificate2 Certificate, string? StoreName)? FindCertificateByHashString ( string hash )
	{
		if ( string.IsNullOrEmpty( hash ) )
			throw new ArgumentNullException( nameof( hash ) );

		using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

		store.Open( OpenFlags.ReadOnly );

		var certificate = store.Certificates
			.FirstOrDefault( x => x.GetCertHashString().Equals( hash, StringComparison.InvariantCultureIgnoreCase ) );

		// .Find( X509FindType.FindByThumbprint, hash, false )

		if ( certificate is not null )
			return (certificate, store.Name);
		return null;
	}
}
