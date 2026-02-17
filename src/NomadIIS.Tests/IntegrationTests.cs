using Microsoft.Web.Administration;
using System;
using System.Collections.Generic;
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

	[Fact]
	public async Task JobWithVirtualDirectories ()
	{
		var jobHcl = """
			job "job-with-virtual-dirs" {
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

			          virtualDirectory {
			            alias = "static"
			            path = "C:\\inetpub\\wwwroot"
			          }

			          virtualDirectory {
			            alias = "uploads"
			            path = "C:\\Windows\\Temp"
			          }
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

			var app = iis.Website( poolAndWebsiteName ).Application( "/" );
			app.ShouldExist();
			app.ShouldHaveVirtualDirectory( "/static" );
			app.ShouldHaveVirtualDirectory( "/uploads" );
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
	public async Task JobWithMultipleBindings ()
	{
		var jobHcl = """
			job "job-with-multiple-bindings" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "app" {
			    count = 1

			    network {
			      port "httplabel" {}
			      port "altport" {}
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

			        binding {
			          type = "http"
			          port = "altport"
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
			var website = iis.Website( poolAndWebsiteName );
			website.ShouldExist();
			website.ShouldHaveBindingCount( 2 );
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
	public async Task JobWithHostnameBinding ()
	{
		var jobHcl = """
			job "job-with-hostname" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "app" {
			    count = 1

			    network {
			      port "httplabel" {
			        static = 8888
			      }
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
			          hostname = "testapp.local"
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
			var website = iis.Website( poolAndWebsiteName );
			website.ShouldExist();
			website.Binding( 0 ).HasHostname( "testapp.local" );
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
	public async Task JobWithCertificateThumbprint ()
	{
		var certificateFile = Path.GetTempFileName() + ".pfx";

		var certificateThumbprint = CertificateHelper.GenerateSelfSignedCertificate(
			"NomadIISTestThumbprint", TimeSpan.FromDays( 2 ), certificateFile, "super#secure" );

		// Install the certificate to the store
		var installResult = await CertificateHelper.InstallPfxCertificateAsync( certificateFile, "super#secure" );

		if ( installResult is null )
			Assert.Fail( "Failed to install certificate" );

		var jobHcl = $$"""
			job "https-job-with-thumbprint" {
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
			            thumbprint = "{{certificateThumbprint}}"
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

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );

		_fixture.AccessIIS( iis =>
		{
			iis.AppPool( poolAndWebsiteName ).ShouldNotExist();
			iis.Website( poolAndWebsiteName ).ShouldNotExist();
		} );

		// Clean up certificate from store
		await CertificateHelper.UninstallCertificatesAsync( new HashSet<string> { certificateThumbprint } );
	}

	[Fact]
	public async Task JobWithNoManagedCode ()
	{
		var jobHcl = """
			job "job-with-no-managed-code" {
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
			          managed_runtime_version = ""
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
			appPool.ShouldHaveManagedRuntimeVersion( "" );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWithClassicPipelineMode ()
	{
		var jobHcl = """
			job "job-with-classic-pipeline" {
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
			          managed_pipeline_mode = "Classic"
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
			appPool.ShouldHaveManagedPipelineMode( ManagedPipelineMode.Classic );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWithOnDemandStartMode ()
	{
		var jobHcl = """
			job "job-with-ondemand-start" {
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
			          start_mode = "OnDemand"
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
			appPool.ShouldHaveStartMode( StartMode.OnDemand );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWithCertificatePemFiles ()
	{
		var certFile = Path.GetFullPath( @"Data\certificates\cert1.pem" );
		var keyFile = Path.GetFullPath( @"Data\certificates\cert1.key.pem" );

		var jobHcl = $$"""
			job "https-job-with-pem-cert" {
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
			            cert_file = "{{certFile.Replace( "\\", "\\\\" )}}"
			            key_file = "{{keyFile.Replace( "\\", "\\\\" )}}"
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
		} );

		var allocation = await _fixture.ReadAllocationAsync( allocations[0].Id );

		Assert.NotNull( allocation );

		var appPort = allocation.Resources.Networks[0].DynamicPorts.First( x => x.Label == "httplabel" ).Value;

		var serverCertificate = _fixture.GetServerCertificate( "localhost", appPort );

		Assert.NotNull( serverCertificate );

		Assert.Equal( "CN=localhost", serverCertificate.Subject );

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
	public async Task JobWithQueueLengthAndTimeouts ()
	{
		var jobHcl = """
			job "job-with-queue-and-timeouts" {
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
			          queue_length = 2000
			          start_time_limit = "2m"
			          shutdown_time_limit = "1m30s"
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
			appPool.ShouldHaveQueueLength( 2000 );
			appPool.ShouldHaveStartTimeLimit( TimeSpan.FromMinutes( 2 ) );
			appPool.ShouldHaveShutdownTimeLimit( new TimeSpan( 0, 1, 30 ) );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWith32BitAppOnWin64 ()
	{
		var jobHcl = """
			job "job-with-32bit-app" {
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
			          enable_32bit_app_on_win64 = true
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
			appPool.ShouldHaveEnable32BitAppOnWin64( true );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWithMultipleEnvironmentVariables ()
	{
		var jobHcl = """
			job "job-with-multiple-env-vars" {
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
			        VAR1 = "value1"
			        VAR2 = "value2"
			        VAR3 = "special!@#$%^&*()chars"
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
			iis.AppPool( poolAndWebsiteName ).ShouldHaveEnvironmentVariable( "VAR1", "value1" );
			iis.AppPool( poolAndWebsiteName ).ShouldHaveEnvironmentVariable( "VAR2", "value2" );
			iis.AppPool( poolAndWebsiteName ).ShouldHaveEnvironmentVariable( "VAR3", "special!@#$%^&*()chars" );
		} );

		_output.WriteLine( "Stopping job..." );

		await _fixture.StopJobAsync( jobId );

		_output.WriteLine( "Job stopped." );
	}

	[Fact]
	public async Task JobWithSNIBinding ()
	{
		var certificateFile = Path.GetTempFileName() + ".pfx";

		var certificateThumbprint = CertificateHelper.GenerateSelfSignedCertificate(
			"SNITest", TimeSpan.FromDays( 2 ), certificateFile, "super#secure" );

		var jobHcl = $$"""
			job "job-with-sni" {
			  datacenters = ["dc1"]
			  type = "service"

			  group "app" {
			    count = 1

			    network {
			      port "httplabel" {
			        static = 8889
			      }
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
			          hostname = "snitest.local"

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

			var binding = iis.Website( poolAndWebsiteName ).Binding( 0 );
			binding.IsHttps();
			binding.HasHostname( "snitest.local" );
			binding.CertificateThumbprintIs( certificateThumbprint );
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
	public async Task JobWithIPAddressBinding ()
	{
		var jobHcl = """
			job "job-with-ip-binding" {
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
			          ip = "*"
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
			iis.Website( poolAndWebsiteName ).Binding( 0 ).HasIPAddress( "*" );
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
}
