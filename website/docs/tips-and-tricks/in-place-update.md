# In-Place Updating Nomad IIS

For updating Nomad nodes i would normally suggest to setup a new node, join it to the cluster and then drain the old one.
But for some reasons you may want to update a node in-place. This basically means you'll do the following steps:

1. Download the new Nomad client binary and/or Nomad IIS binary to the node
2. Stop the Nomad client agent
3. Copy and overwrite the existing binaries
4. Restart the Nomad client agent
5. 🙏 that everything is working 😉

You need to know that by default, when a client disconnects, Nomad considers all allocations as *lost* and immediately reschedules the job to another node.
For IIS workloads you may want to keep them running while doing the update because you know the client will be back online in a moment, so there is no need to unnecessarily reschedule the allocation.

For this to happen, you need to specify a *disconnect behavior* by using the [disconnect block](https://developer.hashicorp.com/nomad/docs/job-specification/disconnect) within the job. Here's an example:

```hcl
job "aspnet-sample-app" {
  datacenters = ["dc1"]
  type = "service"

  group "app" {
    count = 1

    # highlight-start
    disconnect {
      lost_after = "1m"
    }
    # highlight-end

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
    
      resources {
        cpu    = 100
        memory = 150
      }
    }
  }
}
```

With this configuration, Nomad will think the job is still running on this node for up to one minute. As soon as the client will be back online within that minute, Nomad will simply reconnect the allocation to the client and everything is fine. There will be no restart of the IIS application.
But when the client will not be back in one minute, Nomad tries to reschedule it to another node, if possible.
Of course you can adjust the `lost_after` timeout according to your needs.
