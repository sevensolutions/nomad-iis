---
sidebar_position: 4
---

# Driver Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| enabled | bool | no | true | Enables/Disables the Nomad IIS Plugin |
| fingerprint_interval | string | no | 30s | Defines the interval how often the plugin should report the driver's fingerprint to Nomad. The smallest possible value is 10s. |
| directory_security | bool | no | true | Enables Directory Permission Management for [Filesystem Isolation](../features/filesystem-isolation.md). |
| allowed_target_websites | string[] | no | *none* | A list of IIS websites which are allowed to be used as [target_website](../features/existing-website.md). An asterisk (*\**) may be used as a wildcard to allow any website. |
| udp_logger_port | number | no | 0 | The local UDP port where the driver is listening for log-events which will be shipped to the Nomad client. The value 0 will disable this feature. Please read the details [here](../features/udp-logging.md). |

**Example**

```hcl
plugin "nomad_iis" {
  #args = ["--port 1234"] # Optional. To change the static port. The default is 5003.
  #args = ["--port 0"] # Optional. To use a random port
  config {
    enabled = true,
    fingerprint_interval = "30s",
    directory_security = true
    allowed_target_websites = [ "Default Web Site" ]
  }
}
```
