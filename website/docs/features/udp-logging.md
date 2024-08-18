---
sidebar_position: 6
---

# 💬 UDP Logging

:::caution
This feature is considered experimental and not very well tested yet.
:::

Unfortunately, IIS doesn't attach a Console to the *w3wp* processes and therefore *STDOUT* and *STDERR* streams are not available.
As a solution, *nomad-iis* can provide a UDP-endpoint and ship those log messages to the Nomad-Client.

The UDP log-sink exposes two more environment variables:

| Name | Description |
|---|---|
| `NOMAD_STDOUT_UDP_LOCAL_PORT` | The local port the appender has to use. Only messages from this port get received and forwarded to nomad. |
| `NOMAD_STDOUT_UDP_REMOTE_PORT` | The remote port of the log-sink where log events must be sent to. |

Please note, that you need to configure your app's logging provider to log to this UDP endpoint.
Here is an example log4net-appender on how to log to the UDP log-sink:

```xml
<appender name="UdpAppender" type="log4net.Appender.UdpAppender">
    <localPort value="${NOMAD_STDOUT_UDP_LOCAL_PORT}" />
    <remoteAddress value="127.0.0.1" />
    <remotePort value="${NOMAD_STDOUT_UDP_REMOTE_PORT}" />
    <layout type="log4net.Layout.PatternLayout, log4net">
        <conversionPattern value="%d{dd.MM.yy HH:mm:ss.fff} %-5p [%-8t] %-35logger - %m%newline" />
    </layout>
</appender>
```