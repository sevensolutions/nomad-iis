using CliWrap;
using CliWrap.Buffered;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace NomadIIS.IntegrationTests;

public class UnitTest1 : IClassFixture<NomadIISFixture>
{
	private readonly NomadIISFixture _fixture;
	private readonly ITestOutputHelper _output;

	public UnitTest1 ( NomadIISFixture fixture, ITestOutputHelper output )
	{
		_fixture = fixture;
		_output = output;
	}

	[Fact]
	public async Task SubmitSimpleJob_PoolAndWebsiteShouldBeRunning ()
	{
		var jobHcl = """
			job "iis-simple" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "iis-simple" {
			    count = 1

			    network {
			      port "httplabel" {}
			    }

			    task "iis-simple" {
			      driver = "iis"

			      config {
			        application {
			          path = "C:\\inetpub\\wwwroot"
			        }

			        binding {
			          type = "http"
			          port = "httplabel"
			        }
			      }
			    }
			  }
			}
			""";

		_output.WriteLine( "Submitting job..." );

		var jobId = await _fixture.ScheduleJobAsync( jobHcl );

		_output.WriteLine( $"Job Id: {jobId}" );

		var allocations = await _fixture.ListJobAllocationsAsync( jobId );

		if ( allocations is null || allocations.Length == 0 )
			Assert.Fail( "No job allocations" );

		var poolAndWebsiteName = $"nomad-{allocations[0].Id}-iis-simple";

		_output.WriteLine( $"AppPool and Website Name: {poolAndWebsiteName}" );

		_fixture.AccessIIS( sm =>
		{
			Assert.True(
				sm.ApplicationPoolExists( poolAndWebsiteName ),
				$"No application pool with name \"{poolAndWebsiteName}\" found in IIS." );
			Assert.True(
				sm.WebsiteExists( poolAndWebsiteName ),
				$"No website with name \"{poolAndWebsiteName}\" found in IIS." );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );

		_fixture.AccessIIS( sm =>
		{
			Assert.False(
				sm.ApplicationPoolExists( poolAndWebsiteName ),
				$"Application pool with name \"{poolAndWebsiteName}\" still exists in IIS." );
			Assert.False(
				sm.WebsiteExists( poolAndWebsiteName ),
				$"Website with name \"{poolAndWebsiteName}\" still exists in IIS." );
		} );
	}

	[Fact]
	public async Task JobWithEnvVars_PoolShouldHaveEnvVars ()
	{
		var jobHcl = """
			job "iis-simple" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "iis-simple" {
			    count = 1

			    network {
			      port "httplabel" {}
			    }

			    task "iis-simple" {
			      driver = "iis"

			      config {
			        application {
			          path = "C:\\inetpub\\wwwroot"
			        }

			        binding {
			          type = "http"
			          port = "httplabel"
			        }
			      }

				  env {
			        MY_VARIABLE = "hello"
				  }
			    }
			  }
			}
			""";

		_output.WriteLine( "Submitting job..." );

		var jobId = await _fixture.ScheduleJobAsync( jobHcl );

		_output.WriteLine( $"Job Id: {jobId}" );

		var allocations = await _fixture.ListJobAllocationsAsync( jobId );

		if ( allocations is null || allocations.Length == 0 )
			Assert.Fail( "No job allocations" );

		var poolAndWebsiteName = $"nomad-{allocations[0].Id}-iis-simple";

		_output.WriteLine( $"AppPool and Website Name: {poolAndWebsiteName}" );

		_fixture.AccessIIS( sm =>
		{
			Assert.True(
				sm.ApplicationPoolExists( poolAndWebsiteName ),
				$"No application pool with name \"{poolAndWebsiteName}\" found in IIS." );

			Assert.Equal( "hello", sm.GetApplicationPoolEnvironmentVariable( poolAndWebsiteName, "MY_VARIABLE" ) );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}
}
