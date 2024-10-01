﻿using CliWrap;
using CliWrap.Buffered;
using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace NomadIIS.Tests;

public sealed class NomadIISFixture : IAsyncLifetime
{
	private readonly HttpClient _httpClient;
#if MANAGEMENT_API
	private readonly HttpClient _apiHttpClient;
#endif
	private CancellationTokenSource _ctsNomad = new CancellationTokenSource();
	private Thread? _nomadThread;

	public NomadIISFixture ()
	{
		_httpClient = new HttpClient()
		{
			BaseAddress = new Uri( "http://localhost:4646/v1/" ),
			Timeout = TimeSpan.FromSeconds( 10 )
		};

#if MANAGEMENT_API
		_apiHttpClient = new HttpClient()
		{
			BaseAddress = new Uri( "http://localhost:5004/api/v1/" ),
			Timeout = TimeSpan.FromMinutes( 3 ),
			DefaultRequestHeaders =
			{
				{ "X-Api-Key", "12345" }
			}
		};
#endif
	}

	public HttpClient HttpClient => _httpClient;

	public async Task InitializeAsync ()
	{
		var nomadDirectory = Path.GetFullPath( @"..\..\..\..\..\nomad" );
		var dataDirectory = Path.Combine( nomadDirectory, "data" );

		var pluginDir = Environment.GetEnvironmentVariable( "TEST_PLUGIN_DIRECTORY" );
		if ( string.IsNullOrEmpty( pluginDir ) )
			pluginDir = @"..\..\..\..\NomadIIS\bin\Debug\net8.0";

		var pluginDirectory = Path.GetFullPath( pluginDir );

#if MANAGEMENT_API
		var configFile = Path.GetFullPath( @"Data\configs\with_api.hcl" );
#else
		var configFile = Path.GetFullPath( @"Data\configs\default.hcl" );
#endif

		_nomadThread = new Thread( async () =>
		{
			var stdout = new StringBuilder();
			var stderr = new StringBuilder();

			var nomadCommand = Cli.Wrap( Path.Combine( nomadDirectory, "nomad.exe" ) )
				.WithArguments( $"agent -dev -config=\"{configFile}\" -plugin-dir=\"{pluginDirectory}\"" )
				.WithWorkingDirectory( nomadDirectory )
				.WithStandardOutputPipe( PipeTarget.ToDelegate( line =>
				{
					stdout.AppendLine( line );
					Debug.WriteLine( line );
				} ) )
				.WithStandardOutputPipe( PipeTarget.ToDelegate( line =>
				{
					stderr.AppendLine( line );
					Debug.WriteLine( line );
				} ) );

			try
			{
				var result = await nomadCommand.ExecuteBufferedAsync( _ctsNomad.Token );
			}
			catch ( Exception ex )
			{
				Debug.WriteLine( ex.Message );
			}
		} );

		_nomadThread.Start();

		await TryUntilAsync( async () =>
		{
			var health = await GetAgentHealthAsync();

			if ( health is not null && health.Server.Ok && health.Client.Ok )
				return health;

			return null;
		} );
	}

	public Task DisposeAsync ()
	{
		_httpClient.Dispose();

		_ctsNomad.Cancel();

		_nomadThread?.Join();

		var nomadIisProcesses = Process.GetProcessesByName( "nomad_iis" );
		foreach ( var p in nomadIisProcesses )
			p.Kill();

		return Task.CompletedTask;
	}

	private static async Task<T> TryUntilAsync<T> ( Func<Task<T>> action )
	{
		var i = 15;
		while ( i >= 0 )
		{
			await Task.Delay( 2000 );

			try
			{
				var result = await action();
				if ( result is not null )
					return result;
			}
			catch
			{
			}
			finally
			{
				i--;
			}
		}

		throw new TimeoutException();
	}

	public Task<AgentHealthResponse?> GetAgentHealthAsync ()
		=> _httpClient.GetFromJsonAsync<AgentHealthResponse>( "agent/health" );

	public async Task<string> ScheduleJobAsync ( string jobHcl, bool waitUntilRunning = true )
	{
		var response = await _httpClient.PostAsJsonAsync( "jobs/parse", new ParseJobRequest()
		{
			JobHcl = jobHcl,
			Canonicalize = false
		} );

		var parsedJob = await response.Content.ReadFromJsonAsync<ParseJobResponse>();

		var jobId = parsedJob?.Data["ID"]?.ToString();
		if ( string.IsNullOrEmpty( jobId ) )
			throw new InvalidDataException( "Invalid job spec" );

		var createJobHttpResponse = await _httpClient.PostAsJsonAsync( "jobs", new CreateJobRequest()
		{
			Job = parsedJob!.Data
		} );

		var createJobResponse = await createJobHttpResponse.Content.ReadFromJsonAsync<CreateJobResponse>();

		if ( waitUntilRunning )
		{
			await TryUntilAsync( async () =>
			{
				var job = await ReadJobAsync( jobId );

				if ( job is not null && job.Status == JobStatus.Running )
					return job;

				return null;
			} );

			// Wait a bit to let the task stabilize
			await Task.Delay( 3000 );
		}

		return jobId;
	}

	public async Task StopJobAsync ( string jobId )
	{
		await _httpClient.DeleteAsync( $"job/{jobId}?purge=true" );

		await TryUntilAsync<bool?>( async () =>
		{
			try
			{
				var job = await ReadJobAsync( jobId );

				if ( job is not null && job.Status == JobStatus.Dead )
					return true;

				return null;
			}
			catch ( HttpRequestException ex )
			{
				if ( ex.StatusCode == System.Net.HttpStatusCode.NotFound )
					return true;

				return null;
			}
		} );
	}

	public Task<JobResponse?> ReadJobAsync ( string jobId )
		=> _httpClient.GetFromJsonAsync<JobResponse>( $"job/{jobId}" );

	public Task<JobAllocationResponse[]?> ListJobAllocationsAsync ( string jobId )
		=> _httpClient.GetFromJsonAsync<JobAllocationResponse[]>( $"job/{jobId}/allocations" );

	public Task<AllocationResponse?> ReadAllocationAsync ( string allocId )
		=> _httpClient.GetFromJsonAsync<AllocationResponse>( $"allocation/{allocId}" );

	public void AccessIIS ( Action<IisHandle> action )
	{
		using var handle = new IisHandle();

		action( handle );
	}

	public X509Certificate? GetServerCertificate ( string hostName, int port )
	{
		// Establish a TCP connection to the server
		using var client = new TcpClient( hostName, port );

		using var sslStream = new SslStream( client.GetStream(), false, ValidateServerCertificate, null );

		// Initiate the SSL handshake
		sslStream.AuthenticateAsClient( hostName );

		// Get the server's certificate
		return sslStream?.RemoteCertificate;

		bool ValidateServerCertificate ( object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors ) => true;
	}

#if MANAGEMENT_API
	public async Task<byte[]> TakeScreenshotAsync ( string allocId, string taskName )
	{
		var response = await _apiHttpClient.GetAsync( $"allocs/{allocId}/{taskName}/screenshot" );

		response.EnsureSuccessStatusCode();

		using var ms = new MemoryStream();
		await response.Content.CopyToAsync( ms );
		return ms.ToArray();
	}
#endif
}
