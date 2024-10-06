# Working with Certificates

*Nomad IIS* supports the use of TLS Certificates for HTTPS. Currently, you have the following options:

## Options

### Use a pre-installed Certificate

This is the easiest way to use certificates with *Nomad IIS*. An operator needs to install the required certificate on every client node.
You then just specify the certificate's *thumbprint* in the job.

```hcl
job "static-sample-app" {
  group "app" {
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      config {
        application {
          path = "..."
        }
    
        binding {
          # highlight-next-line
          type = "https"
          port = "httplabel"
          
          # highlight-start
          certificate {
            thumbprint = "005ce633b71c48f2345b533fd8e259f154e45ed6"
          }
          # highlight-end
        }
      }
    }
  }
}
```

:::tip
You may need to constrain the job in a way, so that it will only be placed on client nodes, having this certificate installed, or the allocation will fail.
:::

### Install Certificate from File

Another option is to install a specific certificate along with the allocation. Here's an example:

```hcl
job "static-sample-app" {
  group "app" {
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      config {
        application {
          path = "..."
        }
    
        binding {
          # highlight-next-line
          type = "https"
          port = "httplabel"
          
          # highlight-start
          certificate {
            pfx_file = "${NOMAD_SECRETS_DIR}/mycertificate.pfx"
            password = "super#secret"
          }
          # highlight-end
        }
      }
    }
  }
}
```

:::tip
It's up to you where you get the certificate file from. It can be included with the app files, downloaded as a separate [artifact](https://developer.hashicorp.com/nomad/docs/job-specification/artifact). If you want to [render a certificate via template from vault](https://developer.hashicorp.com/nomad/docs/job-specification/template#vault-integration), see the next option.
:::

### Use Vault PKI to Issue a Certificate

It is also possible to use [HashiCorp Vault](https://www.vaultproject.io/) to issue a certificate on-demand.
The idea is that we render the certificate using a [template stanza](https://developer.hashicorp.com/nomad/docs/job-specification/template).

:::caution
It is important to render both, the public and the private key, in the same template.
Otherwise we would get two different certificates issued.  
It is also important to specify `private_key_format` as `pkcs8`.
:::

```hcl
job "static-sample-app" {
  # highlight-start
  vault {
    role         = "pkitest"
    policies     = ["pkitest"]
    change_mode  = "noop"
    env          = false
    disable_file = true
  }
  # highlight-end

  group "app" {
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      # highlight-start
      template {
        data = <<EOH
{{- with pkiCert "pki/test/issue/Test" "common_name=localhost" "private_key_format=pkcs8" -}}
{{ .Cert }}
{{ if .Key }}
{{ .Key }}
{{ end }}
{{ end }}
EOH
        destination = "${NOMAD_SECRETS_DIR}/certificate.pem"
        change_mode = "restart"
      }
      # highlight-end

      config {
        application {
          path = "..."
        }
    
        binding {
          # highlight-next-line
          type = "https"
          port = "httplabel"
          
          # highlight-start
          certificate {
            cert_file = "${NOMAD_SECRETS_DIR}/certificate.pem"
            key_file = "${NOMAD_SECRETS_DIR}/certificate.pem"
          }
          # highlight-end
        }
      }
    }
  }
}
```

### Self Signed Certificate

For quick testing, it may be helpful to use a self-signed certificate.
*Nomad IIS* can provision one on-demand as shown in the following example.
These certificates will have a lifetime of one year.

```hcl
job "static-sample-app" {
  group "app" {
    network {
      port "httplabel" {}
    }

    task "app" {
      driver = "iis"

      config {
        application {
          path = "..."
        }
    
        binding {
          # highlight-next-line
          type = "https"
          port = "httplabel"
          
          # highlight-start
          certificate {
            use_self_signed = true
          }
          # highlight-end
        }
      }
    }
  }
}
```

:::caution
This is usefull for testing HTTPS and *MUST NOT* be used in production.
:::

## Usefull Information

### Where can i find the installed certificates?

All certificates, managed and installed by *Nomad IIS*, are installed into the *My Certificates* store of the local machine.
The easiest way to inspect them is through the *IIS Management Console*.  
Just select the server-node and navigate to *Server Certificates*. Each certificate will be prefixed with *[MBN]*, which is short for *Managed by Nomad*.

### Are the certificates being uninstalled when no longer needed?

Yes of course. They will be uninstalled when the allocation is stopped, but keep in mind, that multiple allocation may use the same certificate.
This means, that the certificate will be removed, once the last allocation, with a binding to it, has been stopped.
