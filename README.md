# HashiCorp Nomad IIS Task Driver

[![Build](https://img.shields.io/github/actions/workflow/status/sevensolutions/nomad-iis/.github%2Fworkflows%2Fbuild.yml?logo=github&label=Build&color=green)](https://github.com/sevensolutions/nomad-iis/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/sevensolutions/nomad-iis?label=Release)](https://github.com/sevensolutions/nomad-iis/releases/latest)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/sevensolutions/nomad-iis/blob/main/LICENSE)

<p align="center" style="text-align:center;">
  <a href="https://github.com/sevensolutions/nomad-iis">
    <img alt="Nomad IIS Logo" src="artwork/logo.svg" width="600" />
  </a>
</p>

This repository contains a task driver for [HashiCorp Nomad](https://www.nomadproject.io/) to run web-applications in IIS on Windows machines. Unlike most other Nomad task drivers, this one is written in the C# language using ASP.NET 7.
It uses the *Microsoft.Web.Administration*-API to communicate with IIS.
Feel free to use it as-is or as a reference implementation for your own C#-based Nomad-plugins.

## 🎉 Features

| Feature | Status | Details |
|---|---|---|
| Single Web App per Nomad Task | ✔ | The Task Driver creates an IIS Application Pool and Website for every Nomad Task in the job specification. |
| Multiple Applications | ✔ | Support for multiple sub-applications below the website. |
| Virtual Directories | ✔ | Support for multiple *virtual directories* below an application. |
| HTTP Bindings | ✔ | |
| HTTPS Bindings | ✔ | [GH-3](https://github.com/sevensolutions/nomad-iis/issues/3) |
| Environment Variables | ✔ | [Details](#-environment-variables) |
| Resource Statistics | ✔ | |
| Logging | ❌ | [GH-6](https://github.com/sevensolutions/nomad-iis/issues/6) |
| Signals with `nomad alloc signal` | ✔ | [Details](#-supported-signals) |
| Exec (Shell Access) | ❌ | I'am playing around a little bit but don't want to give you hope :/. See [GH-15](https://github.com/sevensolutions/nomad-iis/issues/15) for status. |
| Filesystem Isolation | 🔶 | [Details](#-filesystem-isolation) |
| Nomad Networking | ❌ | |

## ⚙ Driver Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| enabled | bool | no | true | Enables/Disables the Nomad IIS Plugin |
| fingerprint_interval | string | no | 30s | Defines the interval how often the plugin should report the driver's fingerprint to Nomad. The smallest possible value is 10s. |
| directory_security | bool | no | true | Enables Directory Permission Management for [Filesystem Isolation](#-filesystem-isolation). |
| allowed_target_websites | string[] | no | *none* | A list of IIS websites which are allowed to be used as [target_website](#-using-an-existing-website). An asterisk (*\**) may be used as a wildcard to allow any website. |

**Example**

```hcl
plugin "nomad_iis" {
  config {
    enabled = true,
    fingerprint_interval = "30s",
    directory_security = true
    allowed_target_websites = [ "Default Web Site" ]
  }
}
```

## ⚙ Task Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| *application* | block list | yes | *none* | Defines one more applications. See *application* schema below for details. |
| target_website | string | no | *none* | Specifies an existing target website. In this case the driver will not create a new website but instead use the existing one where it provisions the virtual applications only. Please read the details [here]([Details](#-using-an-existing-website)). |
| managed_pipeline_mode | string | no | *IIS default* | Valid options are *Integrated* or *Classic* |
| managed_runtime_version | string | no | *IIS default* | Valid options are *v4.0*, *v2.0*, *None* |
| start_mode | string | no | *IIS default* | Valid options are *OnDemand* or *AlwaysRunning* |
| idle_timeout | string | no | *IIS default* | The AppPool idle timeout in the form *HH:mm:ss* or *[00w][00d][00h][00m][00s]* |
| disable_overlapped_recycle | bool | no | *IIS default* | Defines whether two AppPools are allowed to run while recycling |
| periodic_restart | string | no | *IIS default* | The AppPool periodic restart interval in the form *HH:mm:ss* or *[00w][00d][00h][00m][00s]* |
| *binding* | block list | yes | *none* | Defines one or two port bindings. See *binding* schema below for details. |

### `application` Block Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| path | string | yes | *none* | Defines the path of the web application, containing the application files |
| alias | string | no | / | Defines an optional alias at which the application should be hosted below the website. If not set, the application will be hosted at the website level. |
| enable_preload | bool | no | *IIS default* | Specifies whether the application should be pre-loaded. |
| *virtual_directory* | block list | no | *none* | Defines optional virtual directories below this application. See *virtual_directory* schema below for details. |

### `virtual_directory` Block Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| alias | string | yes | *none* | Defines the alias of the virtual directory |
| path | string | yes | *none* | Defines the path of the virtual directory |

### `binding` Block Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| type | string | yes | *none* | Defines the protocol of the port binding. Allowed values are *http* or *https*. |
| port | string | yes | *none* | Defines the port label of a `network` block configuration |
| hostname | string | no | *IIS default* | Only listens to the specified hostname |
| require_sni | bool | no | *IIS default* | Defines whether SNI (Server Name Indication) is required |
| ip_address | string | no | *IIS default* | Specifies the IP-Address of the interface to listen on |
| certificate_hash | string | no | *none* | Specifies the hash of the certificate to use when using type=https |

**Example**

```hcl
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
        application {
          path = "C:\\inetpub\\wwwroot"
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

## 🌎 Environment Variables

All System Environment Variables available to the Nomad Client will be applied to the Application Pool.
You can supply additional ones by using the [`env` Block](https://developer.hashicorp.com/nomad/docs/job-specification/env) in the `task` stanza.

## ✨ Supported Signals

The Nomad IIS driver supports the following signals:

| Signal | Description |
|---|---|
| `SIGHUP` or `RECYCLE` | Recycles the Application Pool |

To send a *RECYCLE* signal, run:

```
nomad alloc signal -s RECYCLE <allocation> <task>
```

Details about the command can be found [here](https://developer.hashicorp.com/nomad/docs/commands/alloc/signal).

## 🛡 Filesystem Isolation

Because there is no `chroot` on Windows, filesystem isolation is only handled via permissions.
For every AppPool, IIS creates a dedicated AppPool Service Account which is only allowed to access it's own directories. See commits of [GH-5](https://github.com/sevensolutions/nomad-iis/issues/5) for details.

Given a job spec with two tasks, the following table depicts the permissions for each AppPool *task1* and *task2* inside the [allocation directory](https://developer.hashicorp.com/nomad/docs/concepts/filesystem).

| Directory | Access Level |
|---|---|
| `/alloc` | No Access |
| `/alloc/data` | Full Access for *task1* and *task2* |
| `/alloc/logs` | Full Access for *task1* and *task2* |
| `/alloc/tmp` | Full Access for *task1* and *task2* |
| `/task1/local` | Full Access for *task1* |
| `/task1/private` | No Access |
| `/task1/secrets` | Read Only for *task1*, No Access for *task2*, no file listing |
| `/task1/tmp` | Full Access for *task1* |
| `/task2/local` | Full Access for *task2* |
| `/task2/private` | No Access |
| `/task2/secrets` | Read Only for *task2*, No Access for *task1*, no file listing |
| `/task2/tmp` | Full Access for *task2* |

## 🌐 Using an existing Website

By specifying a *target_website* in the task configuration you can re-use an existing website managed outside of nomad.
In this case the driver will not create a new website but instead use the existing one where it provisions the virtual applications only.

Note that there're a few restrictions when using a target_website:

- The feature [needs to be enabled](#-driver-configuration).
- Re-using an existing website managed by nomad (owned by a different job or task), is not allowed.
- Bindings and other website-related configuration will have no effect.
- You need to make sure you constrain your jobs to nodes having this target_website available, otherwise the job will fail.
- You cannot create a root-application when using a target_website.

## 🛠 How to Compile

Run the setup command to download the nomad binary.

```
.\setup.ps1
```

Build the project by running the following command:

```
cd src
dotnet build
```

Of course you can also compile with Visual Studio :)

## 🐛 How to Debug locally

There is a launch-profile to run nomad in dev-mode which automatically loads the driver plugin.
Open Visual Studio, select the *Nomad* launch profile and press *F5*.

Note: To debug the driver itself, you need to attach the debugger to the nomad_iis.exe process manually.

## 🎁 How to build Release version

Run the *Release.pubxml* publish profile from Visual Studio. This will create a single binary exe called *nomad_iis.exe*.

## 🚧 TODOs and Known Issues

Check the [Open Issues here](https://github.com/sevensolutions/nomad-iis/issues).
