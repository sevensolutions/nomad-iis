using System.Security.Cryptography.X509Certificates;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NomadIIS.Services;

internal static class CertificateHelper
{
	private const string NomadCertificateFriendlyName = "Installed by Nomad IIS";

	private static readonly Regex _safeThumbprintRegex = new Regex( @"[^\da-fA-F]" );

	private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim( 1, 1 );

	public static async Task<(X509Certificate2 Certificate, string? StoreName)?> InstallCertificateAsync ( string filePath, string? password )
	{
		if ( string.IsNullOrEmpty( filePath ) )
			throw new ArgumentNullException( nameof( filePath ) );

		await _semaphore.WaitAsync();

		string thumbprint;

		try
		{
			using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

			store.Open( OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite );

			var certificate = new X509Certificate2( filePath, password );

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

	private static string MakeSafeThumbprint ( string thumbprint )
	{
		// Strip any non-hexadecimal values and make uppercase
		return _safeThumbprintRegex.Replace( thumbprint, string.Empty ).ToUpper();
	}
}
