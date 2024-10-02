#if MANAGEMENT_API
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NomadIIS.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace NomadIIS.ManagementApi;

[Route( "/api" )]
public sealed class ManagementApiController : Controller
{
	private readonly ManagementService _managementService;

	public ManagementApiController ( ManagementService managementService )
	{
		_managementService = managementService;
	}

	[HttpGet( "v1/allocs/{allocId}/{taskName}/status" )]
	public async Task<IActionResult> GetStatus ( string allocId, string taskName )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		var status = await taskHandle.GetStatusAsync();

		return Ok( status );
	}

	[HttpPut( "v1/allocs/{allocId}/{taskName}/start" )]
	public async Task<IActionResult> StartAppPool ( string allocId, string taskName )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		await taskHandle.StartAppPoolAsync();

		return Ok();
	}
	[HttpPut( "v1/allocs/{allocId}/{taskName}/stop" )]
	public async Task<IActionResult> StopAppPool ( string allocId, string taskName )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		await taskHandle.StopAppPoolAsync();

		return Ok();
	}
	[HttpPut( "v1/allocs/{allocId}/{taskName}/recycle" )]
	public async Task<IActionResult> RecycleAppPool ( string allocId, string taskName )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		await taskHandle.RecycleAppPoolAsync();

		return Ok();
	}

	[HttpGet( "v1/allocs/{allocId}/{taskName}/fs/{path}" )]
	public async Task<IActionResult> GetFileAsync ( string allocId, string taskName, string path )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		path = HttpUtility.UrlDecode( path );

		await taskHandle.DownloadFileAsync( HttpContext.Response, path );

		return Ok();
	}
	[HttpPut( "v1/allocs/{allocId}/{taskName}/fs/{path}" )]
	public async Task<IActionResult> PutFileAsync ( string allocId, string taskName, string path, [FromQuery] bool clean = false )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		path = HttpUtility.UrlDecode( path );

		var isZip = HttpContext.Request.ContentType == "application/zip";

		await taskHandle.UploadFileAsync( HttpContext.Request.Body, isZip, path, false, clean );

		return Ok();
	}
	[HttpPatch( "v1/allocs/{allocId}/{taskName}/fs/{path}" )]
	public async Task<IActionResult> PatchFileAsync ( string allocId, string taskName, string path, [FromQuery] bool clean = false )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		path = HttpUtility.UrlDecode( path );

		var isZip = HttpContext.Request.ContentType == "application/zip";

		await taskHandle.UploadFileAsync( HttpContext.Request.Body, isZip, path, true, clean );

		return Ok();
	}
	[HttpDelete( "v1/allocs/{allocId}/{taskName}/fs/{path}" )]
	public async Task<IActionResult> DeleteFileAsync ( string allocId, string taskName, string path )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		path = HttpUtility.UrlDecode( path );

		await taskHandle.DeleteFileAsync( path );

		return Ok();
	}


	[HttpGet( "v1/allocs/{allocId}/{taskName}/screenshot" )]
	public async Task<IActionResult> GetScreenshotAsync ( string allocId, string taskName, [FromQuery] string path = "/", CancellationToken cancellationToken = default )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		var screenshot = await taskHandle.TakeScreenshotAsync( path, cancellationToken );

		if ( screenshot is null )
			return NotFound();

		return File( screenshot, "image/png", $"screenshot_{allocId}_{taskName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.png" );
	}

	[HttpGet( "v1/allocs/{allocId}/{taskName}/procdump" )]
	public async Task<IActionResult> GetProcdumpAsync ( string allocId, string taskName, CancellationToken cancellationToken = default )
	{
		var taskHandle = _managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

		if ( taskHandle is null )
			return NotFound();

		var dumpFile = new FileInfo( Path.GetTempFileName() + ".dmp" );

		try
		{
			await taskHandle.TakeProcessDump( dumpFile, cancellationToken );

			// Stream the file to the client
			await Results
				.File( dumpFile.FullName, "application/octet-stream", $"procdump_{allocId}_{taskName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.dmp" )
				.ExecuteAsync( HttpContext );

			// Not needed but we need to return something
			return Ok();
		}
		finally
		{
			if ( dumpFile.Exists )
				dumpFile.Delete();
		}
	}
}
#endif
