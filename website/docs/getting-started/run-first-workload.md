---
sidebar_position: 3
---

# Run your first Workload

Now you're ready to submit your first workload.

Open your Nomad cluster's Web UI and submit the following job. Of course you can also submit it using Nomad's CLI. Choose whatever you prefer.

:::note
The following example downloads a very simple HTML app from this repository.
Feel free to inspect the ZIP before running the job.
:::

```hcl
job "static-sample-app" {
  datacenters = ["dc1"]
  type = "service"

  group "app" {
    count = 1
  
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      artifact {
        source = "https://github.com/sevensolutions/nomad-iis/raw/main/examples/static-sample-app.zip"
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
    
      resources {
        cpu    = 100
        memory = 20
      }
    }
  }
}
```