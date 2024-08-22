---
sidebar_position: 3
---

# ✨ Signals

The Nomad IIS driver supports the following signals:

| Signal | Description |
|---|---|
| `SIGHUP` or `RECYCLE` | Recycles the Application Pool |
| `SIGINT` or `SIGKILL` | Stops and removes the Application. Note: When sending this signal manually, the job gets re-scheduled. |

To send a *RECYCLE* signal, run:

```
nomad alloc signal -s RECYCLE <allocation> <task>
```

Details about the command can be found [here](https://developer.hashicorp.com/nomad/docs/commands/alloc/signal).