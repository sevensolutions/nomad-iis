using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NomadIIS.IntegrationTests
{
	public class UnitTest1
	{
		[Fact]
		public async Task Test1 ()
		{
			var nomadDirectory = Path.GetFullPath( @"..\..\..\..\..\nomad" );
			var dataDirectory = Path.Combine( nomadDirectory, "data" );
			var pluginDirectory = Path.GetFullPath( @"..\..\..\..\NomadIIS\bin\Debug\net8.0" );
			var configFile = Path.GetFullPath( @"Data\serverAndClient.hcl" );

			//var nomadCommand = Cli.Wrap( Path.Combine( nomadDirectory, "nomad.exe" ) )
			//	.WithArguments( $"agent -config=\"{configFile}\" -data-dir=\"{dataDirectory}\" -plugin-dir=\"{pluginDirectory}\"" )
			//	.WithWorkingDirectory( nomadDirectory );
			var nomadCommand = Cli.Wrap( Path.Combine( nomadDirectory, "nomad.exe" ) )
				.WithArguments( $"agent -dev -plugin-dir=\"{pluginDirectory}\"" )
				.WithWorkingDirectory( nomadDirectory );

			var ctsNomad = new CancellationTokenSource();

			var nomadTask = nomadCommand.ExecuteBufferedAsync( ctsNomad.Token );

			var testTask = Task.Run( async () =>
			{
				Console.WriteLine( "Waiting for nomad to start..." );

				await Task.Delay( 30_000 );

				using var httpClient = new HttpClient()
				{
					BaseAddress = new Uri( "http://localhost:4646/" )
				};

				var jobHcl = File.ReadAllText( @"Data\simple-job.hcl" );
				var request = new Dictionary<string, object>()
				{
					{ "JobHCL", jobHcl },
					{ "Canonicalize", false }
				};

				var r = await httpClient.PostAsJsonAsync( "v1/jobs/parse", request );
				var jobJson = await r.Content.ReadAsStringAsync();

				var jobJsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>( jobJson );

				request = new Dictionary<string, object>()
				{
					{ "Job", jobJsonObject }
				};

				var r2 = await httpClient.PostAsJsonAsync( "v1/jobs", request );
				var response2 = await r2.Content.ReadAsStringAsync();
			} );

			await Task.WhenAny( nomadTask, testTask );

			Console.WriteLine( "Stopping nomad..." );

			ctsNomad.Cancel();
		}
	}
}
