job "iis-test" {
  datacenters = ["dc1"]
  type = "service"

  group "iis-test" {
    count = 1
    
    network {
      port "httplabel" {}
    }

    task "iis-test" {
      driver = "iis"

      config {
        path = "P:\\work\\WebApplication1\\WebApplication1"
        
        binding {
          type = "http"
          port = "httplabel"
        }
      }
      
      env {
        my_key = "my-value"
      }

      resources {
        cpu    = 800
        memory = 20
      }
    }
  }
}
