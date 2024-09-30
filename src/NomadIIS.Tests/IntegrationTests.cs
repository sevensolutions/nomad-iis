using Microsoft.Web.Administration;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
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

	[Fact]
	public async Task TestHttps ()
	{
		var certificateFile = Path.GetTempFileName() + ".pfx";

		var certificateThumbprint = CertificateHelper.GenerateSelfSignedCertificate( "localhost", certificateFile, "super#secure" );

		var jobHcl = $$"""
			job "https-job-with-cert-file" {
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
			          type = "https"
			          port = "httplabel"

			          certificate {
			            file = "{{certificateFile.Replace( "\\", "\\\\" )}}"
			            password = "super#secure"
			          }
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

			iis.Website( poolAndWebsiteName ).Binding( 0 ).IsHttps();
			iis.Website( poolAndWebsiteName ).Binding( 0 ).CertificateThumbprintIs( certificateThumbprint );
		} );

		//var allocation = await _fixture.ReadAllocationAsync( allocations[0].Id );

		//var appPort = allocation.Resources.Networks[0].DynamicPorts.First( x => x.Label == "httplabel" ).Value;

		//using ( HttpClient client = new HttpClient() )
		//{
		//	using ( HttpResponseMessage response = await client.GetAsync( $"https://localhost:{appPort}" ) )
		//	{
		//		// Get Certificate Here
		//		var cert = ServicePointManager.FindServicePoint( new Uri( $"https://localhost:{appPort}" ) ).Certificate;
		//		//
		//		using ( HttpContent content = response.Content )
		//		{
		//			string result = await content.ReadAsStringAsync();
		//		}
		//	}
		//}

		//_output.WriteLine( "Stopping job..." );

		//await _fixture.StopJobAsync( jobId );

		//_output.WriteLine( "Job stopped." );

		//_fixture.AccessIIS( iis =>
		//{
		//	iis.AppPool( poolAndWebsiteName ).ShouldNotExist();
		//	iis.Website( poolAndWebsiteName ).ShouldNotExist();
		//} );
	}

#if MANAGEMENT_API
	[Fact]
	public async Task ManagementApi_TakeScreenshot ()
	{
		var jobHcl = """
			job "screenshot-job" {
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

		var allocId = allocations[0].Id;
		var poolAndWebsiteName = $"nomad-{allocId}-app";

		_output.WriteLine( $"AppPool and Website Name: {poolAndWebsiteName}" );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolAndWebsiteName ).ShouldExist();
			iis.Website( poolAndWebsiteName ).ShouldExist();
		} );

		var screenshotData = await _fixture.TakeScreenshotAsync( allocId, "app" );

		_output.WriteLine( $"Returned screenshot with a size of {screenshotData.Length / 1024}kB." );

		Assert.True( screenshotData.Length > 10_000, "Invalid screenshot received." );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}
#endif
}
