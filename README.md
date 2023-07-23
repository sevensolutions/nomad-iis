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
| HTTP Bindings | ✔ | |
| HTTPS Bindings | ✔ | [GH-3](https://github.com/sevensolutions/nomad-iis/issues/3) |
| Environment Variables | ✔ | [Details](#-environment-variables) |
| Resource Statistics | ✔ | [GH-13](https://github.com/sevensolutions/nomad-iis/issues/13), CPU isn't working because of a bug. |
| Logging | ❌ | [GH-6](https://github.com/sevensolutions/nomad-iis/issues/6) |
| Signals with `nomad alloc signal` | ✔ | [Details](#-supported-signals) |
| Exec (Shell Access) | ❌ | I'am playing around a little bit but don't want to give you hope :/. See [GH-15](https://github.com/sevensolutions/nomad-iis/issues/15) for status. |
| Filesystem Isolation | 🔶 | [Details](#-filesystem-isolation) |
| Nomad Networking | ❌ | |

## ⚙ Driver Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| enabled | bool | no | true | Enables/Disables the Nomad IIS Plugin |
| stats_interval | string | no | 3s | Defines the interval how often the plugin should report driver statistics to Nomad. The smallest possible value is 1s. |
| fingerprint_interval | string | no | 30s | Defines the interval how often the plugin should report the driver's fingerprint to Nomad. The smallest possible value is 10s. |

**Example**

```hcl
plugin "nomad_iis" {
  config {
    enabled = true,
    stats_interval = "1s"
  }
}
```

## ⚙ Task Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| path | string | yes | *none* | Defines the path of the web application. |
| managed_pipeline_mode | string | no | *IIS default* | Valid options are *Integrated* or *Classic* |
| managed_runtime_version | string | no | *IIS default* | Valid options are *v4.0*, *v2.0*, *None* |
| start_mode | string | no | *IIS default* | Valid options are *OnDemand* or *AlwaysRunning* |
| idle_timeout | string | no | *IIS default* | The AppPool idle timeout in the form *HH:mm:ss* or *[00w][00d][00h][00m][00s]* |
| disable_overlapped_recycle | bool | no | *IIS default* | Defines whether two AppPools are allowed to run while recycling |
| periodic_restart | string | no | *IIS default* | The AppPool periodic restart interval in the form *HH:mm:ss* or *[00w][00d][00h][00m][00s]* |
| *bindings* | block list | no | *none* | Defines one or two port bindings. See *binding* schema below for details. |

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
        path = "C:\\inetpub\\wwwroot"
		
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
