# Constraining Jobs

Here are a few examples of common job constraints which may be helpful when using *Nomad IIS*.

## Run a .NET (Core) App

Running a .NET App on IIS requires the node to have the [.NET Core Hosting Bundle](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/hosting-bundle) installed.
The following constraint will ensure that the job is only placed on nodes, having this bundle installed.

```hcl
constraint {
  attribute = "${attr.driver.iis.iis_aspnet_core_available}"
  value     = true
}
```

:::tip
For running .NET Core Apps on IIS it is also suggested to set `managed_runtime_version` to `None` in the [task configuration](../getting-started/task-configuration.md).
:::

## Using the URL-Rewrite Module

The URL-Rewrite Module also needs to be installed separately by [downloading from here](https://www.iis.net/downloads/microsoft/url-rewrite).
The following constraint will ensure that the job is only placed on nodes, having this module installed.

```hcl
constraint {
  attribute = "${attr.driver.iis.iis_rewrite_module_available}"
  value     = true
}
```
