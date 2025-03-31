---
sidebar_position: 6
---

# 🌐 Using an existing Website

By specifying a *target_website* in the task configuration you can re-use an existing website managed outside of nomad.
In this case the driver will not create a new website but instead use the existing one where it provisions the virtual applications only.

Note that there're a few restrictions when using a target_website:

- The feature [needs to be enabled](../getting-started/driver-configuration.md).
- Re-using an existing website managed by nomad (owned by a different job or task), is not allowed.
- Bindings and other website-related configuration will have no effect.
- You need to make sure you constrain your jobs to nodes having this target_website available, otherwise the job will fail.
- You cannot create a root-application when using a target_website.
