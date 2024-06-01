job "aspnet-sample-app" {
  datacenters = ["dc1"]
  type = "service"

  group "app" {
    count = 1

    # You may want to set this to true
    # prevent_reschedule_on_lost = true
  
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      artifact {
        source = "https://github.com/sevensolutions/nomad-iis/raw/main/examples/aspnet-sample-app.zip"
        destination = "local"
      }

      config {
        application {
          path = "local"
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
