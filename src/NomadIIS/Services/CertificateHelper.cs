using System.Security.Cryptography.X509Certificates;
using System;
using System.Linq;

namespace NomadIIS.Services;

internal static class CertificateHelper
{
	public static X509Certificate2 InstallCertificate ( string filePath, out bool installed, string? password )
	{
		if ( string.IsNullOrEmpty( filePath ) )
			throw new ArgumentNullException( nameof( filePath ) );

		using var store = new X509Store( StoreName.Root, StoreLocation.LocalMachine );

		store.Open( OpenFlags.ReadWrite );

		var certificate = new X509Certificate2( filePath, password );

		var hash = certificate.GetCertHashString();

		var found = store.Certificates.FirstOrDefault(
			x => x.GetCertHashString().Equals( hash, StringComparison.InvariantCultureIgnoreCase ) );

		installed = false;

		if ( found is null )
		{
			store.Add( certificate );

			installed = true;
			found = certificate;
		}

		return found;
	}
	public static void UninstallCertificate ( X509Certificate2 certificate )
	{
		if ( certificate == null )
			throw new ArgumentNullException( nameof( certificate ) );

		using var store = new X509Store( StoreName.Root, StoreLocation.LocalMachine );

		store.Open( OpenFlags.ReadWrite );

		store.Remove( certificate );
	}

	public static (X509Certificate2 Certificate, string? StoreName)? FindCertificateByHashString ( string hash, StoreName storeName = StoreName.My )
	{
		if ( string.IsNullOrEmpty( hash ) )
			throw new ArgumentNullException( nameof( hash ) );

		using var store = new X509Store( storeName, StoreLocation.LocalMachine );

		store.Open( OpenFlags.ReadOnly );

		var certificate = store.Certificates
			.FirstOrDefault( x => x.GetCertHashString().Equals( hash, StringComparison.InvariantCultureIgnoreCase ) );

		if ( certificate is not null )
			return (certificate, store.Name);
		return null;
	}
}
