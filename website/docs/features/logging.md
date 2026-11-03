---
sidebar_position: 9
---

# 📜 Logging

IIS doesn't attach a console to the *w3wp* processes, so *STDOUT* and *STDERR* of an IIS-hosted
app are not available to Nomad the way they are for a normal process. As a result
`nomad alloc logs` and the log view in the Nomad UI stay empty by default.

The `logging` block makes *nomad-iis* tail a log file and forward its content to the Nomad
client, which then treats it exactly like a regular task's stdout.

:::info
This replaces the [UDP log-sink](./udp-logging.md) which was removed in version 0.16.0.
Instead of requiring the app to speak a custom protocol, the driver now reads a plain log
file, which every logging framework can produce out of the box.
:::

## Capturing stdout of an ASP.NET Core app

For apps hosted by the ASP.NET Core Module, the driver can capture the *real* stdout of the
.NET process without any change to the application:

```hcl
config {
  application {
    path = "local"
  }

  logging {
    aspnetcore_stdout = true
  }
}
```

With this enabled, the driver sets `stdoutLogEnabled` and `stdoutLogFile` in the `aspNetCore`
section of the application's *web.config* before the application is started, and then tails
the files the module writes. Anything the app sends to `Console.WriteLine` or to the default
console logger ends up in `nomad alloc logs`.

:::caution
Only applications which already contain an `aspNetCore` section in their *web.config* are
touched. Adding that section to an app which is not hosted by the ASP.NET Core Module would
break it, so if no application of the task has one, log shipping is skipped and a warning is
written to the driver log.

Note that the driver rewrites *web.config* on every allocation. This is harmless for a normal
Nomad workflow, where each allocation starts from freshly extracted artifacts, but be aware of
it if you deploy into a directory you manage by hand.
:::

## Tailing a log file

If the app writes its own log file, point the driver at it. The path is relative to the task
directory and may contain a wildcard, which is what you normally need because most rolling
appenders put a date or a process id in the file name:

```hcl
config {
  application {
    path = "local"
  }

  logging {
    file = "local/logs/app-*.log"
  }
}
```

A matching Serilog configuration would be:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

When the pattern matches several files, all of them are tailed and each one is tracked
separately, so a rotation does not re-send content which has already been shipped.

## Behaviour and limitations

* Only whole lines are forwarded. An incomplete trailing line is held back until its newline
  arrives, which also guarantees a multi-byte UTF-8 character is never split in half.
* A file that shrinks is treated as truncated and is read from the beginning again.
* `max_bytes_per_second` caps the volume forwarded to the Nomad client. Output above that rate
  is delayed, not dropped.
* Everything is forwarded to *stdout*. There is currently no separate *stderr* stream, because
  a log file does not distinguish between the two.
* When the driver restarts and recovers a running task, tailing resumes at the **end** of the
  files that already exist. Nomad has already persisted everything written before the restart,
  so re-reading from the start would duplicate those lines. The trade-off is that lines written
  during the restart window are not shipped.
* Log data is stored twice: once in the app's own log file and once in Nomad's
  `alloc/logs` directory. Writing the app log below the task's `local` directory keeps the
  first copy as short-lived as the allocation itself.
