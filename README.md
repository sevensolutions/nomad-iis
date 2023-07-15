# HashiCorp Nomad IIS Task Driver

[![Build](https://github.com/sevensolutions/nomad-iis/actions/workflows/build.yml/badge.svg)](https://github.com/sevensolutions/nomad-iis/actions/workflows/build.yml)
[![Release](https://img.shields.io/badge/Version-0.1.0-blue)](https://github.com/Roblox/nomad-driver-iis/releases/tag/v0.1.0)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/Roblox/nomad-driver-iis/blob/master/LICENSE)

<p align="center" style="text-align:center;">
  <a href="https://github.com/sevensolutions/nomad-iis">
    <img alt="Nomad IIS Logo" src="artwork/logo.svg" width="600" />
  </a>
</p>

This repository contains a task driver for HashiCorp Nomad to run web-applications in IIS on Windows machines. Unlike most other Nomad task drivers, this one is written in the C# language using ASP.NET 7.
It uses the *Microsoft.Web.Administration*-API to communicate with IIS.
Feel free to use it as-is or as a reference implementation for your own C#-based Nomad-plugins.

## ❓ How it Works

This Task Driver creates an IIS Application Pool and Website for every Nomad Task in a job specification.

## ⚙ Configuration

### Driver Configuration

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

### Task Configuration

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

#### `binding` Block Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| type | string | yes | *none* | Defines the protocol of the port binding. Allowed values are *http* or *https*. |
| port | string | yes | *none* | Defines the port label of a `network` block configuration |
| hostname | string | no | *IIS default* | Only listens to the specified hostname |
| require_sni | bool | no | *IIS default* | Defines whether SNI (Server Name Indication) is required |
| ip_address | string | no | *IIS default* | Specifies the IP-Address of the interface to listen on |
| certificate_hash | string | no | *none* | Specifies the hash of the certificate to use when using type=https |

#### Environment Variables

Environment Variables will be applied to the Application Pool.

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

## ✨ Supported Signals

The Nomad IIS driver supports the following signals:

| Signal | Description |
|---|---|
| `SIGHUP`, `RECYCLE` | Recycles the Application Pool |

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
