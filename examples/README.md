# 🧪 Examples

This directory contains a few sample jobs you can use to test this driver.

> [!NOTE]
> The following examples download ZIP-files from the [latest release](https://github.com/sevensolutions/nomad-iis/releases/latest). The sources are in this directory. Feel free to inspect them before running the job.
>
> To stick to a specific version, replace `releases/latest/download` in the job file with `releases/download/vX.Y.Z`. The sample apps are available from the first release after v1.21.0 onwards.

| Job File | Description |
|---|---|
| static_sample_app.hcl | Runs a very basic and static website containing a single html file. |
| aspnet_sample_app.hcl | Runs a simple ASP.NET application using .NET Framework 4.8. It will show all environment variables the app has access to and you can test file-access permissions. |
| aspnetcore_sample_app.hcl | Runs the same app using ASP.NET Core on .NET 10. Requires the [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) on the Nomad client. |

> [!TIP]
> Please also check [this section](https://nomad-iis.sevensolutions.cc/docs/getting-started/faq) for common known issues.
