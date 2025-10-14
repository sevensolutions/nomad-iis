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
          identity = "ApplicationPoolIdentity"
        }
        applicationPool {
          name = "appA"
          start_mode = "AlwaysRunning"
          identity = "NetworkService"
        }
        applicationPool {
          name = "appB"
          start_mode = "AlwaysRunning"
          identity = "SpecificUser"
          username = "CONTOSO\\WebAppUser"
          password = "SecretPassword123"
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
## Application Pool Identities

Each application pool can be configured to run under a specific identity. This is useful for security scenarios where different applications need different levels of access or need to authenticate as specific users.

### Supported Identity Types

- **`ApplicationPoolIdentity`** (default): Each application pool runs under its own built-in account (IIS AppPool\\{PoolName})
- **`LocalSystem`**: Runs under the local system account with high privileges
- **`LocalService`**: Runs under the local service account with minimal privileges  
- **`NetworkService`**: Runs under the network service account, suitable for accessing network resources
- **`SpecificUser`**: Runs under a specific user account (requires username and optionally password)

### Security Considerations

When using `SpecificUser` identity:
- The username field is required
- The password field is optional and can be omitted for Group Managed Service Accounts (GMSA)
- The driver administrator can restrict which identities and users are allowed via the [driver configuration](../getting-started/driver-configuration.md)

### Example with Different Identities

```hcl
config {
  applicationPool {
    name = "web"
    identity = "ApplicationPoolIdentity" # Default IIS AppPool identity
  }
  applicationPool {
    name = "api"
    identity = "NetworkService" # Can access network resources
  }
  applicationPool {
    name = "backend"
    identity = "SpecificUser"
    username = "DOMAIN\\ServiceAccount"
    password = "SecretPassword"
  }
  applicationPool {
    name = "gmsa"
    identity = "SpecificUser"
    username = "DOMAIN\\GMSAAccount$" # Group Managed Service Account
    # password omitted for GMSA
  }
}
```

## Restrictions and Good to Know

- **Environment Variables**  
Environment Variables are defined using Nomad's [`env`-stanza](https://developer.hashicorp.com/nomad/docs/job-specification/env) outside of the task configuration.
This means that every application pool get's access to all environment variables.
- **Folder Permissions**  
The app pool identity is permitted to read and write the allocation directory according to [this documentation](./filesystem-isolation.md).
If you use multiple application pools with different identities, each identity will be granted the same permissions to the allocation directory. The driver automatically determines the correct identity name for permission assignment based on the configured identity type.
- **Identity Restrictions**  
The driver administrator can restrict which application pool identities and specific users are allowed through the `allowed_apppool_identities` and `allowed_apppool_users` configuration options. These restrictions are enforced at task startup time.
- **Signals**  
*nomad-iis* provides some Nomad signals like `START`, `STOP` or `RECYCLE`. If you're using multiple application pools these signals will affect *all* application pools.
- **Resource Usage**  
*nomad-iis* will simply sum-up the reported resource consumptions when using multiple application pools.
- **Application Pool References**  
Applications can only reference application pools defined within the same task. You cannot reference other application pools.

