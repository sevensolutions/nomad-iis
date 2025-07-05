# HashiCorp Nomad IIS Task Driver

[![Build](https://img.shields.io/github/actions/workflow/status/sevensolutions/nomad-iis/.github%2Fworkflows%2Fbuild.yml?logo=github&label=Build&color=green)](https://github.com/sevensolutions/nomad-iis/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/sevensolutions/nomad-iis?label=Release)](https://github.com/sevensolutions/nomad-iis/releases/latest)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/sevensolutions/nomad-iis/blob/main/LICENSE)

<p align="center" style="text-align:center;">
  <a href="https://github.com/sevensolutions/nomad-iis">
    <img alt="Nomad IIS Logo" src="artwork/logo.svg" width="600" />
  </a>
</p>

A task driver for [HashiCorp Nomad](https://www.nomadproject.io/) to run web-applications in IIS on Windows machines. Unlike most other Nomad task drivers, this one is written in the C# language using ASP.NET 9.
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
| Multiple Application Pools | ✔ | Support for multiple application pools for complex hosting requirements. [Details](https://nomad-iis.sevensolutions.cc/docs/features/multi-application-pools) |
| Virtual Directories | ✔ | Support for multiple *virtual directories* below an application. |
| HTTP Bindings | ✔ | |
| HTTPS Bindings | ✔ | [Details](https://nomad-iis.sevensolutions.cc/docs/features/https) |
| Environment Variables | ✔ | [Details](https://nomad-iis.sevensolutions.cc/docs/features/environment-variables) |
| Resource Statistics | ✔ | |
| Signals | ✔ | [Details](https://nomad-iis.sevensolutions.cc/docs/features/signals) |
| Exec (Shell Access) | ❌ | I'am playing around a little bit but don't want to give you hope :/. See [GH-15](https://github.com/sevensolutions/nomad-iis/issues/15) for status. |
| Filesystem Isolation | 🔶 | [Details](https://nomad-iis.sevensolutions.cc/docs/features/filesystem-isolation) |
| Nomad Networking | ❌ | |
| Management API | ✔ | *Nomad IIS* provides a very powerfull [Management API](https://nomad-iis.sevensolutions.cc/docs/features/management-api) with functionalities like taking a local screenshot or creating a process dump. |

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

Run the *Release.pubxml* or *ReleaseWithMgmtApi.pubxml* publish profile from Visual Studio. This will create a single binary exe called *nomad_iis.exe*.

## 🚧 TODOs and Known Issues

Check the [Open Issues here](https://github.com/sevensolutions/nomad-iis/issues).

## ☕ Support

I put a lot of ❤️ and effort into this project and i want to make it *the best* IIS driver for Nomad.
Every contribution helps me to improve it, fix bugs and develop new features.
Please also dont forget to ★ the repo.
Thank You!

[![](https://img.shields.io/static/v1?label=Sponsor&color=blue&message=%E2%9D%A4&logo=GitHub)](https://github.com/sponsors/sevensolutions)
