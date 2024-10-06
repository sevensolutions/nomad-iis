---
sidebar_position: 5
---

# Task Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| *application* | block list | yes | *none* | Defines one more applications. See *application* schema below for details. |
| target_website | string | no | *none* | Specifies an existing target website. In this case the driver will not create a new website but instead use the existing one where it provisions the virtual applications only. Please read the details [here](../features/existing-website.md). |
| managed_pipeline_mode | string | no | *IIS default* | Valid options are *Integrated* or *Classic* |
| managed_runtime_version | string | no | *IIS default* | Valid options are *v4.0*, *v2.0*, *None* |
| start_mode | string | no | *IIS default* | Valid options are *OnDemand* or *AlwaysRunning* |
| idle_timeout | string | no | *IIS default* | The AppPool idle timeout in the form *HH:mm:ss* or *[00w][00d][00h][00m][00s]* |
| disable_overlapped_recycle | bool | no | *IIS default* | Defines whether two AppPools are allowed to run while recycling |
| periodic_restart | string | no | *IIS default* | The AppPool periodic restart interval in the form *HH:mm:ss* or *[00w][00d][00h][00m][00s]* |
| enable_udp_logging | bool | no | false | Enables a UDP log-sink your application can log to. Please read the details [here](../features/udp-logging.md). |
| permit_iusr | bool | no | true | Specifies whether you want to permit the [IUSR-account](https://learn.microsoft.com/en-us/iis/get-started/planning-for-security/understanding-built-in-user-and-group-accounts-in-iis#understanding-the-new-iusr-account) on the *local* directory. When you disable this, you may need to tweak your *web.config* a bit. Read [this](./faq.md#iusr-account) for details. |
| *binding* | block list | yes | *none* | Defines one or two port bindings. See *binding* schema below for details. |

## `application` Block

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| path | string | yes | *none* | Defines the path of the web application, containing the application files. If this folder is empty, the [Placeholder App](../getting-started/driver-configuration.md) will be copied into. |
| alias | string | no | / | Defines an optional alias at which the application should be hosted below the website. If not set, the application will be hosted at the website level. |
| enable_preload | bool | no | *IIS default* | Specifies whether the application should be pre-loaded. |
| *virtual_directory* | block list | no | *none* | Defines optional virtual directories below this application. See *virtual_directory* schema below for details. |

## `virtual_directory` Block

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| alias | string | yes | *none* | Defines the alias of the virtual directory |
| path | string | yes | *none* | Defines the path of the virtual directory |

## `binding` Block

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| type | string | yes | *none* | Defines the protocol of the port binding. Allowed values are *http* or *https*. |
| port | string | yes | *none* | Defines the port label of a `network` block or a static port like "80". Static ports can only be used when *hostname* is also set. Otherwise use a nomad *network*-stanza to specify the port. |
| hostname | string | no | *IIS default* | Only listens to the specified hostname |
| require_sni | bool | no | *IIS default* | Defines whether SNI (Server Name Indication) is required |
| ip_address | string | no | *IIS default* | Specifies the IP-Address of the interface to listen on |
| *certificate* | block list | no | *none* | Specifies the certificate to use when using type=https. See *certificate* schema below for details. |

## `certificate` Block

:::tip
Also refer to this [advanced documentation](../features/https.md).
:::

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| thumbprint | string | no | *none* | Specifies the thumbprint (hash) of a local and pre-installed certificate. Make sure the certificate is accessible to IIS by installing it to the *My Certificates* store on Local Machine. |
| pfx_file | string | no | *none* | Specifies the path to a local certificate file. The file must be of type *.pfx*. |
| password | string | no | *none* | Specifies the password for the given pfx-certificate file. |
| cert_file | string | no | *none* | Specifies the path to a local certificate file in base64-encoded pem format. When using this option you also need to specify `key_file`. |
| key_file | string | no | *none* | Specifies the path to a local private key file in base64-encoded pkcs8 format. When using this option you also need to specify `cert_file`. |
| use_self_signed | bool | no | false | Set this to true if you want to use a self-signed certificate with a validity of one year. Important: This is not intended for production usage and should only be used for short lived tasks like UI- or Integration tests. |

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