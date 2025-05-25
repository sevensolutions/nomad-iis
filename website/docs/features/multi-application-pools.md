---
sidebar_position: 7
---

# 🗃 Multiple Application Pools

By default, every Nomad-task will result in a single application pool and website. You can even host multiple sub-application within that website which will all share the same application pool by default.

In more complicated setup you might want to separate applications and run them on different application pools.
Starting with *nomad-iis* 0.15.0 this might be possible.

Using multiple application pools might also be necessary when using multiple .NET Core applications in the *in-process hosting model* because this is a [known limitation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-3.0#in-process-hosting-model).

## Example

Here is a full example using multiple application pools. The specified settings within the application pools are just examples. Only the `name` is required.

:::note
Application pool names are limited to 8 characters.
:::

```hcl
job "multi-pool-example" {
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
        # highlight-start
        applicationPool {
          #name = "default" # Can be omitted, because it's the default
          managed_runtime_version = "None"
          start_mode = "AlwaysRunning"
        }
        applicationPool {
          name = "appA"
          start_mode = "AlwaysRunning"
        }
        applicationPool {
          name = "appB"
          start_mode = "AlwaysRunning"
        }
        # highlight-end

        application {
          path = "local/path-to-root-app"
          #application_pool = "default" # Can be omitted and it will use the default app pool
        }
        application {
          alias = "/app-a"
          path = "local/path-to-appA"
          # highlight-next-line
          application_pool = "appA" # References the applicationPool name
        }
        application {
          alias = "/app-b"
          path = "local/path-to-appB"
          # highlight-next-line
          application_pool = "appB"
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
## Restrictions and Good to Know

- **Environment Variables**  
Environment Variables are defined using Nomad's [`env`-stanza](https://developer.hashicorp.com/nomad/docs/job-specification/env) outside of the task configuration.
This means that every application pool get's access to all environment variables.
- **Folder Permissions**  
The app pool identity is permitted to read and write the allocation directory according to [this documentation](./filesystem-isolation.md).
If you use multiple application pools there will be no distinction and every pool will be permitted in the exact same way because the driver doesn't know which application pool needs to access which subfolder.
- **Signals**  
*nomad-iis* provides some Nomad signals like `START`, `STOP` or `RECYCLE`. If you're using multiple application pools these signals will affect *all* application pools.
- **Resource Usage**  
*nomad-iis* will simply sum-up the reported resource consumptions when using multiple application pools.
- **Application Pool References**  
Applications can only reference application pools defined within the same task. You cannot reference other application pools.

