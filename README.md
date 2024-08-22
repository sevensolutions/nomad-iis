# HashiCorp Nomad IIS Task Driver

[![Build](https://img.shields.io/github/actions/workflow/status/sevensolutions/nomad-iis/.github%2Fworkflows%2Fbuild.yml?logo=github&label=Build&color=green)](https://github.com/sevensolutions/nomad-iis/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/sevensolutions/nomad-iis?label=Release)](https://github.com/sevensolutions/nomad-iis/releases/latest)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/sevensolutions/nomad-iis/blob/main/LICENSE)

<p align="center" style="text-align:center;">
  <a href="https://github.com/sevensolutions/nomad-iis">
    <img alt="Nomad IIS Logo" src="artwork/logo.svg" width="600" />
  </a>
</p>

This repository contains a task driver for [HashiCorp Nomad](https://www.nomadproject.io/) to run web-applications in IIS on Windows machines. Unlike most other Nomad task drivers, this one is written in the C# language using ASP.NET 8.
It uses the *Microsoft.Web.Administration*-API to communicate with IIS.
Feel free to use it as-is or as a reference implementation for your own C#-based Nomad-plugins.

> [!NOTE]  
> This document always represents the latest version, which may not have been released yet.  
> Therefore, some features may not be available currently but will be available soon.
> You can use the GIT-Tags to check individual versions.

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
| Logging | ✔ | Experimental UDP logging. See [GH-6](https://github.com/sevensolutions/nomad-iis/issues/6) for details. |
| Signals with `nomad alloc signal` | ✔ | [Details](#-supported-signals) |
| Exec (Shell Access) | ❌ | I'am playing around a little bit but don't want to give you hope :/. See [GH-15](https://github.com/sevensolutions/nomad-iis/issues/15) for status. |
| Filesystem Isolation | 🔶 | [Details](#-filesystem-isolation) |
| Nomad Networking | ❌ | |

## 📚 Documentation

Please see the full documentation [HERE](https://nomad-iis.sevensolutions.cc/).

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
Open Visual Studio, select the *Nomad (Dev)* launch profile and press *F5*.

Note: To debug the driver itself, you need to attach the debugger to the nomad_iis.exe process manually.

## 🎁 How to build Release version

Run the *Release.pubxml* publish profile from Visual Studio. This will create a single binary exe called *nomad_iis.exe*.

## 🚧 TODOs and Known Issues

Check the [Open Issues here](https://github.com/sevensolutions/nomad-iis/issues).

## ☕ Support

You want to support me?

<a href="https://www.buymeacoffee.com/sevensolutions" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" ></a>
