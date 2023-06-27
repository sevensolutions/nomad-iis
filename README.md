# HashiCorp Nomad IIS Task Driver

This repository contains a task driver for HashiCorp Nomad to run web-applications in IIS on Windows machines. Unlike most other Nomad task drivers, this one is written in the C# language using ASP.NET 7.
Feel free to use it as-is or as a reference implementation for your own C#-based Nomad-plugins.

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

## 🌍 How to build Release version

Run the *Release.pubxml* publish profile from Visual Studio. This will create a single binary exe called *nomad_iis.exe*.
