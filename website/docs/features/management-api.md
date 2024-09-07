---
sidebar_position: 7
---

# 🛠 Management API

:::caution
The Management API is only available when using a special binary of *Nomad IIS*.  
Please also note, that the API is experimental and may change in the future.
:::

The Management API is very powerfull and provides additional features, not provided by Nomad directly.
It is meant to be called by some external higher-order service or management tool.

Most endpoints will need you to provide the allocation id which means, you first need to talk to the [Nomad API](https://developer.hashicorp.com/nomad/api-docs/jobs#list-job-allocations) to find that out.

## Enabling the API

You need to enable the Management API by providing a dedicated port as shown below.
In order to use *procdump* you need to accept it's EULA by providing another argument.

```hcl
plugin "nomad_iis" {
  args = ["--management-api-port=5004", "--management-api-key=12345", "--procdump-accept-eula=true"]
  config {
    enabled = true
  }
}
```

## Securing the API

It is highly recommended to provide an API-Key to secure the API.
In this case, every API-call needs to provide this key as `X-Api-Key` header.

## Upload (Push) an Application

```
PUT /api/v1/allocs/{allocId}/upload[?appAlias=...]
```

It is possible to upload an application into a previously deployed allocation. This can be thought as the opposite of Nomad pulling the application from somewhere. This is usefull if you want to run an application shortly, eg. to run UI-tests against.

:::danger
If Nomad reschedules the allocation, all uploaded application files will be lost.
:::

## Application Pool Lifecycle Management

| API | Description |
|---|---|
| `GET /api/v1/allocs/{allocId}/start` | Start the Application Pool |
| `GET /api/v1/allocs/{allocId}/stop` | Stop the Application Pool while keeping the Nomad allocation running |
| `GET /api/v1/allocs/{allocId}/recycle` | Recycle the Application Pool |

## Taking a local Screenshot

```
GET /api/v1/allocs/{allocId}/screenshot[?path=/]
```

:::info
Screenshots will be taken by Playwright, which starts a local Chrome browser. This requires downloading some necessary drivers from the Internet, which means the first request will take a few seconds.
:::

## Taking a Process Dump

```
GET /api/v1/allocs/{allocId}/procdump
```

Sometimes you need to investigate a performance or memory issue and need a process dump of the *w3wp* worker process.
By calling this API, a process dump will be created using *procdump.exe* which will be streamed to the client.
