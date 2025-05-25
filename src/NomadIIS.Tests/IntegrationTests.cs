using Microsoft.Web.Administration;
using System;
using System.IO;
using Xunit.Abstractions;
using NomadIIS.Services;
using System.Linq;

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

			        applicationPool {
			          managed_pipeline_mode = "Integrated"
			          managed_runtime_version = "v4.0"
			          start_mode = "AlwaysRunning"
			          idle_timeout = "45m"
			          disable_overlapped_recycle = true
			          periodic_restart = "1h30m"
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
	public async Task JobWithCertificateFile_WebsiteShouldUseCertificate ()
	{
		var certificateFile = Path.GetTempFileName() + ".pfx";

		var certificateThumbprint = CertificateHelper.GenerateSelfSignedCertificate(
			"NomadIISTest", TimeSpan.FromDays( 2 ), certificateFile, "super#secure" );

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
			            pfx_file = "{{certificateFile.Replace( "\\", "\\\\" )}}"
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

		var allocation = await _fixture.ReadAllocationAsync( allocations[0].Id );

		Assert.NotNull( allocation );

		var appPort = allocation.Resources.Networks[0].DynamicPorts.First( x => x.Label == "httplabel" ).Value;

		var serverCertificate = _fixture.GetServerCertificate( "localhost", appPort );

		Assert.NotNull( serverCertificate );

		Assert.Equal( "CN=NomadIISTest", serverCertificate.Subject );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolAndWebsiteName ).ShouldNotExist();
			iis.Website( poolAndWebsiteName ).ShouldNotExist();
		} );

		// TODO: Certificate should have been removed
	}

	[Fact]
	public async Task JobWithMultipleAppPools ()
	{
		var jobHcl = $$"""
			job "job-with-multiple-apppools" {
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
			        applicationPool {
			          name = "pool-A"
			        }
			        applicationPool {
			          name = "pool-B"
			        }

			        application {
			          path = "C:\\inetpub\\wwwroot"
			        }
			        application {
			          alias = "/app-a"
			          path = "C:\\inetpub\\wwwroot"
			          application_pool = "pool-A"
			        }
			        application {
			          alias = "/app-b"
			          path = "C:\\inetpub\\wwwroot"
			          application_pool = "pool-B"
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

		var websiteName = $"nomad-{allocations[0].Id}-app";
		var poolDefaultName = $"nomad-{allocations[0].Id}-app";
		var poolAName = $"nomad-{allocations[0].Id}-app-pool-A";
		var poolBName = $"nomad-{allocations[0].Id}-app-pool-B";

		_output.WriteLine( $"Website Name: {websiteName}" );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolDefaultName ).ShouldExist();
			iis.AppPool( poolAName ).ShouldExist();
			iis.AppPool( poolBName ).ShouldExist();
			iis.Website( websiteName ).ShouldExist();

			var app = iis.Website( websiteName ).Application( "/" );
			app.ShouldExist();
			app.ShouldRunOnApplicationPool( poolDefaultName );

			app = iis.Website( websiteName ).Application( "/app-a" );
			app.ShouldExist();
			app.ShouldRunOnApplicationPool( poolAName );

			app = iis.Website( websiteName ).Application( "/app-b" );
			app.ShouldExist();
			app.ShouldRunOnApplicationPool( poolBName );
		} );

		var allocation = await _fixture.ReadAllocationAsync( allocations[0].Id );

		Assert.NotNull( allocation );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolDefaultName ).ShouldNotExist();
			iis.AppPool( poolAName ).ShouldNotExist();
			iis.AppPool( poolBName ).ShouldNotExist();
			iis.Website( websiteName ).ShouldNotExist();
		} );
	}

	[Fact]
	public async Task UnusedAppPoolsShouldNotBeCreated ()
	{
		var jobHcl = $$"""
			job "job-with-unused-apppools" {
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
			        applicationPool {
			          name = "unused-A"
			        }
			        applicationPool {
			          name = "unused-B"
			        }
			        applicationPool {
			          name = "pool-B"
			        }

			        application {
			          alias = "/app-b"
			          path = "C:\\inetpub\\wwwroot"
			          application_pool = "pool-B"
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

		var websiteName = $"nomad-{allocations[0].Id}-app";
		var poolDefaultName = $"nomad-{allocations[0].Id}-app";
		var poolUnusedAName = $"nomad-{allocations[0].Id}-app-unused-A";
		var poolUnusedBName = $"nomad-{allocations[0].Id}-app-unused-B";
		var poolBName = $"nomad-{allocations[0].Id}-app-pool-B";

		_output.WriteLine( $"Website Name: {websiteName}" );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolDefaultName ).ShouldNotExist();
			iis.AppPool( poolUnusedAName ).ShouldNotExist();
			iis.AppPool( poolUnusedBName ).ShouldNotExist();
			iis.AppPool( poolBName ).ShouldExist();
			iis.Website( websiteName ).ShouldExist();

			var app = iis.Website( websiteName ).Application( "/app-b" );
			app.ShouldExist();
			app.ShouldRunOnApplicationPool( poolBName );
		} );

		var allocation = await _fixture.ReadAllocationAsync( allocations[0].Id );

		Assert.NotNull( allocation );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolBName ).ShouldNotExist();
			iis.Website( websiteName ).ShouldNotExist();
		} );
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
