job "aspnetcore-sample-app" {
  datacenters = ["dc1"]
  type = "service"

  group "app" {
    count = 1

    # See: https://nomad-iis.sevensolutions.cc/docs/tips-and-tricks/in-place-update
    # disconnect {
    #  lost_after = "1m"
    # }

    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      artifact {
        source = "https://github.com/sevensolutions/nomad-iis/releases/latest/download/aspnetcore-sample-app.zip"
        destination = "local"
      }

      config {
        application {
          path = "local"
        }
		
        application_pool {
          managed_runtime_version = "None"
        }

        binding {
          type = "http"
          port = "httplabel"
        }
      }

      env {
        SAMPLE_KEY = "my-value"
      }

      resources {
        cpu    = 100
        memory = 150
      }
    }
  }
}
