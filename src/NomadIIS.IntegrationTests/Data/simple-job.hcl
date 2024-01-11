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
