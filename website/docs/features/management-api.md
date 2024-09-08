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

```hcl
plugin "nomad_iis" {
  args = ["--management-api-port=5004", "--management-api-key=12345"]
  config {
    enabled = true
  }
}
```

### Securing the API

It is highly recommended to provide an API-Key to secure the API. Specify the key using the `--management-api-key`-argument as shown above.
In this case, every API-call needs to provide this key as `X-Api-Key` header.

## Filesystem Access

### Download a File or Folder

```
GET /api/v1/allocs/{allocId}/{taskName}/fs/{path}
```

This allows you to download a single file or an entire folder from the task directory.
The path needs to be URL-encoded and point to a single file when downloading a file and to a folder, when downloading an entire folder as a ZIP-archive.

### Upload a File or ZIP-Archive

```
PUT /api/v1/allocs/{allocId}/{taskName}/fs/{path}[?clean=true/false]
PATCH /api/v1/allocs/{allocId}/{taskName}/fs/{path}[?clean=true/false]
```

With this API you can upload a single file or an entire ZIP-archive into the specified folder of the task directory.
Make sure you send the correct `Content-Type`-header (`application-zip` for ZIP-files and `application/octet-stream` for files).
The path needs to be URL-encoded and point to a single file when sending a file and to a folder, when sending a ZIP-archive.

Setting the `clean`-parameter to true will delete all files in the target-directory before uploading the new ones. The default is false.

The difference between the `PUT` and `PATCH` method is, that `PUT` will stop the application while uploading the file, whereas `PATCH` will hot-patch the file, keeping the app running. Keep in mind that hot-patching may fail if some files are currently being locked by the worker process.

:::tip
Using these methods it is possible to upload an application into a previously deployed allocation. This can be thought as the opposite of Nomad pulling the application from somewhere. This is usefull if you want to run an application shortly, eg. to run UI-tests against.
:::

:::info
ZIP-archives will be extracted by default. If you want to upload the ZIP-file *as-is*, send it using the `Content-Type` set to `application/octet-stream`.
:::

:::danger
If Nomad reschedules the allocation, all uploaded application files will be lost.
:::

### Examples

**Upload a zipped application to the *local* directory**

```cmd
curl -X PUT \
  -H "X-Api-Key: 12345" \
  -H "Content-Type: application/zip" \
  --data-binary @"C:\Path\To\static-sample-app.zip" \
  http://localhost:5004/api/v1/allocs/e4c0ee58-2e27-2cd6-7ca5-6ef1ed036aad/app/fs/local?clean=true
```

**Download the entire *local* directory as a zip archive**

```cmd
curl -X GET \
  -H "X-Api-Key: 12345" \
  -o "local.zip" \
  http://localhost:5004/api/v1/allocs/e4c0ee58-2e27-2cd6-7ca5-6ef1ed036aad/app/fs/local
```

**Download just a single file from the *local* directory**

```cmd
curl -X GET \
  -H "X-Api-Key: 12345" \
  -o "index.html" \
  http://localhost:5004/api/v1/allocs/e4c0ee58-2e27-2cd6-7ca5-6ef1ed036aad/app/fs/local%2findex.html
```

## Application Pool Lifecycle Management

| API | Description |
|---|---|
| `PUT /api/v1/allocs/{allocId}/{taskName}/start` | Start the Application Pool |
| `PUT /api/v1/allocs/{allocId}/{taskName}/stop` | Stop the Application Pool while keeping the Nomad allocation running |
| `PUT /api/v1/allocs/{allocId}/{taskName}/recycle` | Recycle the Application Pool |

## Taking a local Screenshot

```
GET /api/v1/allocs/{allocId}/{taskName}/screenshot[?path=/]
```

:::info
Screenshots will be taken by Playwright, which starts a local Chrome browser. This requires downloading some necessary drivers from the Internet, which means the first request will take a few seconds.
:::

### Examples

**Take a screenshot**

```cmd
curl -X GET -O \
  -H "X-Api-Key: 12345" \
  http://localhost:5004/api/v1/allocs/e4c0ee58-2e27-2cd6-7ca5-6ef1ed036aad/app/screenshot
```

## Taking a Process Dump

:::info
To use this feature you need to download and install [procdump.exe](https://learn.microsoft.com/en-us/sysinternals/downloads/procdump) to `C:\procdump.exe` or specify a different location in the [driver configuration](../getting-started/driver-configuration.md).  
You also have to agree to the EULA of procdump by setting `accept_eula` to `true`.
:::

```
GET /api/v1/allocs/{allocId}/{taskName}/procdump
```

Sometimes you need to investigate a performance or memory issue and need a process dump of the *w3wp* worker process.
By calling this API, a process dump will be created using *procdump.exe* which will be streamed to the client.

### Examples

**Take a process dump**

```cmd
curl -X GET -O \
  -H "X-Api-Key: 12345" \
  http://localhost:5004/api/v1/allocs/e4c0ee58-2e27-2cd6-7ca5-6ef1ed036aad/app/procdump
```