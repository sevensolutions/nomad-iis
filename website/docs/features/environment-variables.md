---
sidebar_position: 2
---

# 🌎 Environment Variables

All System Environment Variables, available to the Nomad Client, will be applied to the Application Pool.
You can supply additional ones by using the [`env` Block](https://developer.hashicorp.com/nomad/docs/job-specification/env) in the `task` stanza.

## Example

```hcl
job "env-sample-app" {
  datacenters = ["dc1"]
  type = "service"

  group "app" {
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      config {
        application {
          path = "local"
        }

        binding {
          type = "http"
          port = "httplabel"
        }
      }

      # highlight-start
      env {
        ASPNETCORE_ENVIRONMENT = "Production"
        SAMPLE_KEY             = "my-value"
      }
      # highlight-end

      resources {
        cpu    = 100
        memory = 150
      }
    }
  }
}
```

The variables defined above will show up in the Application Pool's *environmentVariables* collection and are picked up by the `w3wp.exe` worker process, so your application can read them the same way it would read any other environment variable (eg. `Environment.GetEnvironmentVariable("SAMPLE_KEY")` in .NET).

:::tip
Nomad also injects its own set of dynamic variables, like `NOMAD_PORT_httplabel`, `NOMAD_ALLOC_DIR` or `NOMAD_IP_httplabel`. These are available to your application as well and can be very useful, for example, to let an ASP.NET Core app know which port it got assigned. See the [full list of predefined Nomad variables](https://developer.hashicorp.com/nomad/docs/runtime/environment).
:::

## Rendering Environment Variables from a Template

If you need to inject secrets (eg. from [Vault](https://developer.hashicorp.com/nomad/docs/job-specification/template#vault-integration)) as environment variables, you can use a [`template` Block](https://developer.hashicorp.com/nomad/docs/job-specification/template) with `env = true`. The rendered key/value pairs will be merged into the task's environment, exactly like the `env` block above.

```hcl
task "app" {
  driver = "iis"

  # highlight-start
  template {
    data = <<EOH
{{- with secret "secret/data/myapp" }}
DATABASE_PASSWORD={{ .Data.data.password }}
{{- end }}
EOH
    destination = "${NOMAD_SECRETS_DIR}/env.txt"
    env         = true
  }
  # highlight-end

  config {
    application {
      path = "local"
    }
  }
}
```

## Good to Know

- **`TMP` and `TEMP`**  
These are always overridden by *Nomad IIS* to point to the task's own temp directory, no matter what you set via `env`. See [Filesystem Isolation](./filesystem-isolation.md) for details.
- **Overriding the Nomad Client's own Environment Variables**  
If a variable already exists in the environment of the Nomad Client process, the value from your `env` block is ignored for that variable, and the Client's value wins instead.
