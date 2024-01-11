job "iis-test" {
  datacenters = ["dc1"]
  type = "service"

  group "iis-test" {
    count = 1

    # You may want to set this to true
    # prevent_reschedule_on_lost = true
    
    network {
      port "httplabel" {}
    }

    task "iis-test" {
      driver = "iis"

      config {
        application {
          path = "C:\\inetpub\\wwwroot"
        }
        application {
          alias = "subapp"
          path = "C:\\inetpub\\wwwroot"
        }
        
        binding {
          type = "http"
          port = "httplabel"
        }
      }
      
      env {
        my_key = "my-value"
      }

      resources {
        cpu    = 100
        memory = 20
      }
    }
  }
}
