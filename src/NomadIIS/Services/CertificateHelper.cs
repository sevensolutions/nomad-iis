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
	private const string CertificatesStateFile = "certificates.state";

	private static Regex _safeThumbprintRegex = new Regex( @"[^\da-fA-F]" );

	private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim( 1, 1 );
	private static Dictionary<string, HashSet<string>>? _state;

	public static async Task<(X509Certificate2 Certificate, string? StoreName)?> InstallCertificateAsync ( string filePath, string websiteName, string? password )
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
				certificate.FriendlyName = "Installed by Nomad-IIS";

				store.Add( certificate );
			}

			await AddWebsiteToStateAsync( thumbprint, websiteName );
		}
		finally
		{
			_semaphore.Release();
		}

		return await FindCertificateByThumbprintAsync( thumbprint );
	}
	public static async Task UninstallCertificatesAsync ( string websiteName )
	{
		if ( string.IsNullOrEmpty( websiteName ) )
			throw new ArgumentNullException( nameof( websiteName ) );

		await _semaphore.WaitAsync();

		try
		{
			var certificateThumbprint = await RemoveWebsiteFromStateAsync( websiteName );
			if ( certificateThumbprint is null )
				return;

			using var store = new X509Store( StoreName.My, StoreLocation.LocalMachine );

			store.Open( OpenFlags.ReadWrite );

			var certificates = store.Certificates
				.Find( X509FindType.FindByThumbprint, certificateThumbprint, false );

			foreach ( var certificate in certificates )
				store.Remove( certificate );
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

	private static async Task AddWebsiteToStateAsync ( string certificateThumbprint, string websiteName )
	{
		await EnsureStateLoadedAsync();

		if ( _state!.TryGetValue( certificateThumbprint, out var websitesArray ) )
		{
			websitesArray ??= new HashSet<string>();
			websitesArray.Add( websiteName );

			_state[certificateThumbprint] = websitesArray;
		}
		else
			_state[certificateThumbprint] = [websiteName];

		await SaveStateAsync();
	}
	private static async Task<string?> RemoveWebsiteFromStateAsync ( string websiteName )
	{
		await EnsureStateLoadedAsync();

		string? thumbprint = null;

		foreach ( var kvp in _state!.ToArray() )
		{
			if ( kvp.Value.Remove( websiteName ) )
			{
				// If we have removed the last website, using this certificate, we can uninstall it.
				if ( kvp.Value.Count == 0 )
				{
					_state!.Remove( kvp.Key );

					thumbprint = MakeSafeThumbprint( kvp.Key );
					break;
				}
			}
		}

		await SaveStateAsync();

		return thumbprint;
	}
	private static async Task EnsureStateLoadedAsync ()
	{
		if ( _state is not null )
			return;

		if ( File.Exists( CertificatesStateFile ) )
		{
			try
			{
				var json = await File.ReadAllTextAsync( CertificatesStateFile );
				if ( !string.IsNullOrEmpty( json ) )
				{
					var rawState = JsonSerializer.Deserialize<Dictionary<string, object>>( json );
					if ( rawState is not null )
					{
						_state = rawState
							.ToDictionary(
								x => x.Key,
								x => ( (JsonElement)x.Value )
									.EnumerateArray()
									.Select( x => x.GetString()! )
									.ToHashSet() );

						return;
					}
				}
			}
			catch ( Exception )
			{
			}
		}

		_state = new Dictionary<string, HashSet<string>>();
	}
	private static async Task SaveStateAsync ()
	{
		var json = JsonSerializer.Serialize( _state, new JsonSerializerOptions() { WriteIndented = true } );
		await File.WriteAllTextAsync( CertificatesStateFile, json );
	}
}
