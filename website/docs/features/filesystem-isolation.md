---
sidebar_position: 5
---

# 🛡 Filesystem Isolation

Because there is no `chroot` on Windows, filesystem isolation is only handled via permissions.
For every AppPool, IIS creates a dedicated AppPool Service Account which is only allowed to access it's own directories.

Given a job spec with two tasks, the following table depicts the permissions for each AppPool *task1* and *task2* inside the [allocation directory](https://developer.hashicorp.com/nomad/docs/concepts/filesystem).

| Directory | Access Level |
|---|---|
| `/alloc` | No Access |
| `/alloc/data` | Full Access for *task1* and *task2* |
| `/alloc/logs` | Full Access for *task1* and *task2* |
| `/alloc/tmp` | Full Access for *task1* and *task2* |
| `/task1/local` | Full Access for *task1* |
| `/task1/private` | No Access |
| `/task1/secrets` | Read Only for *task1*, No Access for *task2*, no file listing |
| `/task1/tmp` | Full Access for *task1* |
| `/task2/local` | Full Access for *task2* |
| `/task2/private` | No Access |
| `/task2/secrets` | Read Only for *task2*, No Access for *task1*, no file listing |
| `/task2/tmp` | Full Access for *task2* |

:::info
When accessing the `%TEMP%` or `%TMP%` environment variable or [`Path.GetTempPath()`](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.gettemppath) within your application you will get the corresponding task's temp-directory (eg. `/task1/tmp`). This means that also temp-files are kept within the tasks allocation directory.
:::
