---
sidebar_position: 5
---

# Task Configuration

| Option                           | Type       | Required | Default Value | Description                                                                                                                                                                                                                                                                                                                                                               |
| -------------------------------- | ---------- | -------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| _applicationPool_                | block list | no       | _none_        | Defines one more application pools. See _applicationPool_ schema below for details.                                                                                                                                                                                                                                                                                       |
| _application_                    | block list | yes      | _none_        | Defines one more applications. See _application_ schema below for details.                                                                                                                                                                                                                                                                                                |
| target_website                   | string     | no       | _none_        | Specifies an existing target website. In this case the driver will not create a new website but instead use the existing one where it provisions the virtual applications only. Please read the details [here](../features/existing-website.md).                                                                                                                          |
| enable_udp_logging               | bool       | no       | false         | Enables a UDP log-sink your application can log to. Please read the details [here](../features/udp-logging.md).                                                                                                                                                                                                                                                           |
| permit_iusr                      | bool       | no       | true          | Specifies whether you want to permit the [IUSR-account](https://learn.microsoft.com/en-us/iis/get-started/planning-for-security/understanding-built-in-user-and-group-accounts-in-iis#understanding-the-new-iusr-account) on the _local_ directory. When you disable this, you may need to tweak your _web.config_ a bit. Read [this](./faq.md#iusr-account) for details. |
| _binding_                        | block list | yes      | _none_        | Defines one or two port bindings. See _binding_ schema below for details.                                                                                                                                                                                                                                                                                                 |
| ~~managed_pipeline_mode~~        | string     | no       | _IIS default_ | Valid options are _Integrated_ or _Classic_                                                                                                                                                                                                                                                                                                                               |
| ~~enable_32bit_app_on_win64~~    | bool       | no       | _IIS default_ | When true, enables a 32-bit application to run on a computer that runs a 64-bit version of Windows.                                                                                                                                                                                                                                                                       |
| ~~managed_runtime_version~~      | string     | no       | _IIS default_ | Valid options are _v4.0_, _v2.0_, _None_                                                                                                                                                                                                                                                                                                                                  |
| ~~start_mode~~                   | string     | no       | _IIS default_ | Valid options are _OnDemand_ or _AlwaysRunning_                                                                                                                                                                                                                                                                                                                           |
| ~~idle_timeout~~                 | string     | no       | _IIS default_ | The AppPool idle timeout in the form _HH:mm:ss_ or _[00w][00d][00h][00m][00s]_                                                                                                                                                                                                                                                                                            |
| ~~disable_overlapped_recycle~~   | bool       | no       | _IIS default_ | Defines whether two AppPools are allowed to run while recycling                                                                                                                                                                                                                                                                                                           |
| ~~periodic_restart~~             | string     | no       | _IIS default_ | The AppPool periodic restart interval in the form _HH:mm:ss_ or _[00w][00d][00h][00m][00s]_                                                                                                                                                                                                                                                                               |
| ~~service_unavailable_response~~ | string     | no       | _IIS default_ | If this is set to `HttpLevel` and the app pool isn't running, HTTP.sys will return a 503 http-error. On the other hand if this is set to `TcpLevel` and the app pool isn't running, HTTP.sys will simply drop the connection. This may be useful when using external load balancers.                                                                                      |
| ~~queue_length~~                 | number     | no       | _IIS default_ | Indicates to HTTP.sys how many requests to queue for an application pool before rejecting future requests.                                                                                                                                                                                                                                                                |
| ~~start_time_limit~~             | string     | no       | _IIS default_ | Specifies the time in the form _[00w][00d][00h][00m][00s]_ that IIS waits for an application pool to start. If the application pool does not startup within the startupTimeLimit, the worker process is terminated and the rapid-fail protection count is incremented.                                                                                                    |
| ~~shutdown_time_limit~~          | string     | no       | _IIS default_ | Specifies the time in the form _[00w][00d][00h][00m][00s]_ that the W3SVC service waits after it initiated a recycle. If the worker process does not shut down within the shutdownTimeLimit, it will be terminated by the W3SVC service.                                                                                                                                  |

:::note
Strikethrough configuration options will be removed in the next version. Please use the [`applicationPool` block](#applicationpool-block) instead.
:::

## `applicationPool` Block

:::info
In nomad-iis up to version including 0.14.x, all application pool related settings were specified on the [root configuration](#task-configuration).
Starting with version 0.15.0 you need to put these onto a dedicated `applicationPool` block but you can omit the name if you only need a single app pool. This will be the case most of the time.

Please also read [this section](../features/multi-application-pools.md) for more details about using multiple application pools.

<details>
<summary>Short Example</summary>

```hcl
config {
  applicationPool {
    managed_runtime_version = "None"
  }
}
```

</details>
:::

| Option                       | Type       | Required | Default Value | Description                                                                                                                                                                                                                                                                          |
| ---------------------------- | ---------- | -------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| name                         | string     | no       | `default`     | Specifies an alias name for the application pool. This can be used to reference the application pool within the `application` block. It is limited to 8 characters.                                                                                                                  |
| managed_pipeline_mode        | string     | no       | _IIS default_ | Valid options are _Integrated_ or _Classic_                                                                                                                                                                                                                                          |
| enable_32bit_app_on_win64    | bool       | no       | _IIS default_ | When true, enables a 32-bit application to run on a computer that runs a 64-bit version of Windows.                                                                                                                                                                                  |
| managed_runtime_version      | string     | no       | _IIS default_ | Valid options are _v4.0_, _v2.0_, _None_                                                                                                                                                                                                                                             |
| start_mode                   | string     | no       | _IIS default_ | Valid options are _OnDemand_ or _AlwaysRunning_                                                                                                                                                                                                                                      |
| idle_timeout                 | string     | no       | _IIS default_ | The AppPool idle timeout in the form _HH:mm:ss_ or _[00w][00d][00h][00m][00s]_                                                                                                                                                                                                       |
| disable_overlapped_recycle   | bool       | no       | _IIS default_ | Defines whether two AppPools are allowed to run while recycling                                                                                                                                                                                                                      |
| periodic_restart             | string     | no       | _IIS default_ | The AppPool periodic restart interval in the form _HH:mm:ss_ or _[00w][00d][00h][00m][00s]_                                                                                                                                                                                          |
| service_unavailable_response | string     | no       | _IIS default_ | If this is set to `HttpLevel` and the app pool isn't running, HTTP.sys will return a 503 http-error. On the other hand if this is set to `TcpLevel` and the app pool isn't running, HTTP.sys will simply drop the connection. This may be useful when using external load balancers. |
| queue_length                 | number     | no       | _IIS default_ | Indicates to HTTP.sys how many requests to queue for an application pool before rejecting future requests.                                                                                                                                                                           |
| start_time_limit             | string     | no       | _IIS default_ | Specifies the time in the form _[00w][00d][00h][00m][00s]_ that IIS waits for an application pool to start. If the application pool does not startup within the startupTimeLimit, the worker process is terminated and the rapid-fail protection count is incremented.               |
| shutdown_time_limit          | string     | no       | _IIS default_ | Specifies the time in the form _[00w][00d][00h][00m][00s]_ that the W3SVC service waits after it initiated a recycle. If the worker process does not shut down within the shutdownTimeLimit, it will be terminated by the W3SVC service.                                             |
| _extension_                  | block list | no       | _none_        | Allows for additional attributes for properties not explicitly supported. See _extension_ schema below for details.                                                                                                                                                                  |

## `application` Block

| Option                      | Type       | Required | Default Value | Description                                                                                                                                                                                |
| --------------------------- | ---------- | -------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| path                        | string     | yes      | _none_        | Defines the path of the web application, containing the application files. If this folder is empty, the [Placeholder App](../getting-started/driver-configuration.md) will be copied into. |
| alias                       | string     | no       | `/`           | Defines an optional alias at which the application should be hosted below the website. If not set, the application will be hosted at the website level.                                    |
| application_pool            | string     | no       | `default`     | References an application pool on which this application should be executed.                                                                                                               |
| enable_preload              | bool       | no       | _IIS default_ | Specifies whether the application should be pre-loaded.                                                                                                                                    |
| service_auto_start_enabled  | bool       | no       | _IIS default_ | Specifies whether the application should be automatically started.                                                                                                                         |
| service_auto_start_provider | string     | no       | _IIS default_ | Specifies the name of the autostart provider if service_auto_start_enabled is set to true.                                                                                                 |
| _virtual_directory_         | block list | no       | _none_        | Defines optional virtual directories below this application. See _virtual_directory_ schema below for details.                                                                             |
| _extension_                 | block list | no       | _none_        | Allows for additional attributes for properties not explicitly supported. See _extension_ schema below for details.                                                                        |

## `virtual_directory` Block

| Option      | Type       | Required | Default Value | Description                                                                                                         |
| ----------- | ---------- | -------- | ------------- | ------------------------------------------------------------------------------------------------------------------- |
| alias       | string     | yes      | _none_        | Defines the alias of the virtual directory                                                                          |
| path        | string     | yes      | _none_        | Defines the path of the virtual directory                                                                           |
| _extension_ | block list | no       | _none_        | Allows for additional attributes for properties not explicitly supported. See _extension_ schema below for details. |

## `extension` Block

:::info
In the event that a configurable property is not supported by a block type, an extension may be used. Each extension will set a corresponding attribute via the [IIS setting schema](<https://learn.microsoft.com/en-us/previous-versions/iis/settings-schema/aa347559(v=vs.90)>). Using an unsupported attribute may cause IIS failures.

| Option | Type   | Required | Default Value | Description                 |
| ------ | ------ | -------- | ------------- | --------------------------- |
| name   | string | yes      | _none_        | Defines the attribute name  |
| value  | string | yes      | _none_        | Defines the attribute value |

## `binding` Block

| Option        | Type       | Required | Default Value | Description                                                                                                                                                                                    |
| ------------- | ---------- | -------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| type          | string     | yes      | _none_        | Defines the protocol of the port binding. Allowed values are _http_ or _https_.                                                                                                                |
| port          | string     | yes      | _none_        | Defines the port label of a `network` block or a static port like "80". Static ports can only be used when _hostname_ is also set. Otherwise use a nomad _network_-stanza to specify the port. |
| hostname      | string     | no       | _IIS default_ | Only listens to the specified hostname                                                                                                                                                         |
| require_sni   | bool       | no       | _IIS default_ | Defines whether SNI (Server Name Indication) is required                                                                                                                                       |
| ip_address    | string     | no       | _IIS default_ | Specifies the IP-Address of the interface to listen on                                                                                                                                         |
| _certificate_ | block list | no       | _none_        | Specifies the certificate to use when using type=https. See _certificate_ schema below for details.                                                                                            |

## `certificate` Block

:::tip
Also refer to this [advanced documentation](../features/https.md).
:::

| Option          | Type   | Required | Default Value | Description                                                                                                                                                                                                                  |
| --------------- | ------ | -------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| thumbprint      | string | no       | _none_        | Specifies the thumbprint (hash) of a local and pre-installed certificate. Make sure the certificate is accessible to IIS by installing it to the _My Certificates_ store on Local Machine.                                   |
| pfx_file        | string | no       | _none_        | Specifies the path to a local certificate file. The file must be of type _.pfx_.                                                                                                                                             |
| password        | string | no       | _none_        | Specifies the password for the given pfx-certificate file.                                                                                                                                                                   |
| cert_file       | string | no       | _none_        | Specifies the path to a local certificate file in base64-encoded pem format. When using this option you also need to specify `key_file`.                                                                                     |
| key_file        | string | no       | _none_        | Specifies the path to a local private key file in base64-encoded pkcs8 format. When using this option you also need to specify `cert_file`.                                                                                  |
| use_self_signed | bool   | no       | false         | Set this to true if you want to use a self-signed certificate with a validity of one year. Important: This is not intended for production usage and should only be used for short lived tasks like UI- or Integration tests. |

## Example

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
