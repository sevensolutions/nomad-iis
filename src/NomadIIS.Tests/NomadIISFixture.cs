using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NomadIIS.IntegrationTests;

public sealed class NomadIISFixture : IAsyncLifetime
{
	private readonly HttpClient _httpClient;
	private CancellationTokenSource _ctsNomad = new CancellationTokenSource();
	private Thread? _nomadThread;

	public NomadIISFixture ()
	{
		_httpClient = new HttpClient()
		{
			BaseAddress = new Uri( "http://localhost:4646/v1/" ),
			Timeout = TimeSpan.FromSeconds( 10 )
		};
	}

	public HttpClient HttpClient => _httpClient;

	public async Task InitializeAsync ()
	{
		var nomadDirectory = Path.GetFullPath( @"..\..\..\..\..\nomad" );
		var dataDirectory = Path.Combine( nomadDirectory, "data" );
		var pluginDirectory = Path.GetFullPath( @"..\..\..\..\NomadIIS\bin\Debug\net8.0" );
		var configFile = Path.GetFullPath( @"Data\serverAndClient.hcl" );

		_nomadThread = new Thread( async () =>
		{
			var nomadCommand = Cli.Wrap( Path.Combine( nomadDirectory, "nomad.exe" ) )
				.WithArguments( $"agent -dev -plugin-dir=\"{pluginDirectory}\"" )
				.WithWorkingDirectory( nomadDirectory )
				.WithValidation( CommandResultValidation.None );

			var result = await nomadCommand.ExecuteBufferedAsync( _ctsNomad.Token );

			Debug.WriteLine( result.StandardOutput );
			Debug.WriteLine( result.StandardError );
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
		var request = new Dictionary<string, object>()
			{
				{ "JobHCL", jobHcl },
				{ "Canonicalize", false }
			};

		var r = await _httpClient.PostAsJsonAsync( "jobs/parse", request );
		var jobJson = await r.Content.ReadAsStringAsync();

		var jobJsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>( jobJson );

		request = new Dictionary<string, object>()
			{
				{ "Job", jobJsonObject }
			};

		var createJobHttpResponse = await _httpClient.PostAsJsonAsync( "jobs", request );
		var createJobResponse = await createJobHttpResponse.Content.ReadFromJsonAsync<CreateJobResponse>();

		var jobId = jobJsonObject["ID"].ToString();

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

		await Task.Delay( 3000 );
	}

	public Task<JobResponse?> ReadJobAsync ( string jobId )
		=> _httpClient.GetFromJsonAsync<JobResponse>( $"job/{jobId}" );

	public Task<JobAllocationResponse[]?> ListJobAllocationsAsync ( string jobId )
		=> _httpClient.GetFromJsonAsync<JobAllocationResponse[]>( $"job/{jobId}/allocations" );

	public void AccessIIS ( Action<ServerManager> action )
	{
		using var serverManager = new ServerManager();

		action( serverManager );
	}
}

public sealed class AgentHealthResponse
{
	[JsonPropertyName( "client" )]
	public AgentHealth Client { get; set; }
	[JsonPropertyName( "server" )]
	public AgentHealth Server { get; set; }
}
public sealed class AgentHealth
{
	[JsonPropertyName( "ok" )]
	public bool Ok { get; set; }
	[JsonPropertyName( "message" )]
	public string Message { get; set; }
}

public sealed class CreateJobResponse
{
	[JsonPropertyName( "EvalID" )]
	public string EvalId { get; set; }
}

public sealed class JobResponse
{
	[JsonPropertyName( "ID" )]
	public string Id { get; set; }
	[JsonPropertyName( "Name" )]
	public string Name { get; set; }
	[JsonPropertyName( "Status" )]
	public JobStatus Status { get; set; }
}

public sealed class JobAllocationResponse
{
	[JsonPropertyName( "ID" )]
	public string Id { get; set; }
	[JsonPropertyName( "Name" )]
	public string Name { get; set; }
}

[JsonConverter( typeof( JsonStringEnumConverter<JobStatus> ) )]
public enum JobStatus
{
	[JsonPropertyName( "pending" )]
	Pending,
	[JsonPropertyName( "running" )]
	Running,
	[JsonPropertyName( "dead" )]
	Dead
}
