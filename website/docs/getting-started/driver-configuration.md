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
| placeholder_app_path | string | no | C:\\inetpub\\wwwroot | Specifies the path to an optional placeholder app. The files of this folder will be copied into the allocation directory when the application path, specified in the job spec, is empty. This may be usefull to show some kind of maintenance-page until the real app is pushed using [the management API](../features/management-api.md#push-app). By default the blue default IIS page will be copied and you can set this to `null` to not copy anything. |
| *procdump* | block list | no | *none* | Defines settings for procdump. See *procdump* schema below for details. Only available when using the nomad_iis.exe including the Management API. |

## `procdump` Block Configuration

| Option | Type | Required | Default Value | Description |
|---|---|---|---|---|
| binary_path | string | no | C:\\procdump.exe | Configures the path to procdump.exe. |
| accept_eula | bool | no | false | If you want to use procdump you need to accept it's EULA. |

**Example**

```hcl
plugin "nomad_iis" {
  #args = ["--port=1234"] # Optional. To change the static port. The default is 5003.
  #args = ["--port=0"] # Optional. To use a random port
  config {
    enabled = true,
    fingerprint_interval = "30s",
    directory_security = true
    allowed_target_websites = [ "Default Web Site" ]

    # Only available when using the nomad_iis binary with Management API
    procdump {
      binary_path = "C:\\procdump.exe"
      accept_eula = true
    }
  }
}
```
