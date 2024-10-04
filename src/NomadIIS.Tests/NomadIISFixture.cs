using CliWrap;
using CliWrap.Buffered;
using Microsoft.Web.Administration;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NomadIIS.Tests;

public sealed class NomadIISFixture : IAsyncLifetime
{
	private readonly IMessageSink _messageSink;

	private readonly HttpClient _httpClient;
#if MANAGEMENT_API
	private readonly HttpClient _apiHttpClient;
#endif
	private CancellationTokenSource _ctsNomad = new CancellationTokenSource();
	private Thread? _nomadThread;

	public NomadIISFixture ( IMessageSink messageSink )
	{
		_messageSink = messageSink;

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

		_messageSink.OnMessage( new DiagnosticMessage( "Data Directory: {0}", dataDirectory ) );
		_messageSink.OnMessage( new DiagnosticMessage( "Plugin Directory: {0}", pluginDirectory ) );
		_messageSink.OnMessage( new DiagnosticMessage( "Config File: {0}", configFile ) );

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
				_messageSink.OnMessage( new DiagnosticMessage( ex.Message ) );
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
		}

		return jobId;
	}

	public async Task StopJobAsync ( string jobId )
	{
		await _httpClient.DeleteAsync( $"job/{jobId}" );

		await TryUntilAsync( async () =>
		{
			var job = await ReadJobAsync( jobId );

			if ( job is not null && job.Status == JobStatus.Dead )
				return job;

			return null;
		} );
	}

	public Task<JobResponse?> ReadJobAsync ( string jobId )
		=> _httpClient.GetFromJsonAsync<JobResponse>( $"job/{jobId}" );

	public Task<JobAllocationResponse[]?> ListJobAllocationsAsync ( string jobId )
		=> _httpClient.GetFromJsonAsync<JobAllocationResponse[]>( $"job/{jobId}/allocations" );

	public void AccessIIS ( Action<IisHandle> action )
	{
		using var handle = new IisHandle();

		action( handle );
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
