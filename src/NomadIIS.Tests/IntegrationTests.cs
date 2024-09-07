using Microsoft.Web.Administration;
using System;
using Xunit.Abstractions;

namespace NomadIIS.Tests;

public class IntegrationTests : IClassFixture<NomadIISFixture>
{
	private readonly NomadIISFixture _fixture;
	private readonly ITestOutputHelper _output;

	public IntegrationTests ( NomadIISFixture fixture, ITestOutputHelper output )
	{
		_fixture = fixture;
		_output = output;
	}

	[Fact]
	public async Task SubmitSimpleJob_PoolAndWebsiteShouldBeRunning ()
	{
		var jobHcl = """
			job "simple-job" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "app" {
			    count = 1

			    network {
			      port "httplabel" {}
			    }

			    task "app" {
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

		var poolAndWebsiteName = $"nomad-{allocations[0].Id}-app";

		_output.WriteLine( $"AppPool and Website Name: {poolAndWebsiteName}" );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolAndWebsiteName ).ShouldExist();
			iis.Website( poolAndWebsiteName ).ShouldExist();
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolAndWebsiteName ).ShouldNotExist();
			iis.Website( poolAndWebsiteName ).ShouldNotExist();
		} );
	}

	[Fact]
	public async Task JobWithEnvVars_PoolShouldHaveEnvVars ()
	{
		var jobHcl = """
			job "job-with-env-vars" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "app" {
			    count = 1

			    network {
			      port "httplabel" {}
			    }

			    task "app" {
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

		var poolAndWebsiteName = $"nomad-{allocations[0].Id}-app";

		_output.WriteLine( $"AppPool and Website Name: {poolAndWebsiteName}" );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolAndWebsiteName ).ShouldExist();
			iis.AppPool( poolAndWebsiteName ).ShouldHaveEnvironmentVariable( "MY_VARIABLE", "hello" );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWithSettings_PoolShouldHaveSettings ()
	{
		var jobHcl = """
			job "job-with-settings" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "app" {
			    count = 1

			    network {
			      port "httplabel" {}
			    }

			    task "app" {
			      driver = "iis"

			      config {
			        application {
			          path = "C:\\inetpub\\wwwroot"
			        }

			        managed_pipeline_mode = "Integrated"
			        managed_runtime_version = "v4.0"
			        start_mode = "AlwaysRunning"
			        idle_timeout = "45m"
			        disable_overlapped_recycle = true
			        periodic_restart = "1h30m"

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

		var poolAndWebsiteName = $"nomad-{allocations[0].Id}-app";

		_output.WriteLine( $"AppPool and Website Name: {poolAndWebsiteName}" );

		_fixture.AccessIIS( iis =>
		{
			var appPool = iis.AppPool( poolAndWebsiteName );

			appPool.ShouldExist();
			appPool.ShouldHaveManagedPipelineMode( ManagedPipelineMode.Integrated );
			appPool.ShouldHaveManagedRuntimeVersion( "v4.0" );
			appPool.ShouldHaveStartMode( StartMode.AlwaysRunning );
			appPool.ShouldHaveIdleTimeout( TimeSpan.FromMinutes( 45 ) );
			appPool.ShouldHaveDisableOverlappedRecycle( true );
			appPool.ShouldHavePeriodicRestart( new TimeSpan( 1, 30, 0 ) );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}
}
