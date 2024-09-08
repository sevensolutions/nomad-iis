#if MANAGEMENT_API
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NomadIIS.Services;
using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Web;

namespace NomadIIS.ManagementApi;

public sealed class ManagementApiService
{
	public static IEndpointConventionBuilder Map ( WebApplication app )
	{
		var api = app.MapGroup( "/api/v1" );

		var allocsApi = api.MapGroup( "/allocs" );

		allocsApi.MapGet( "{allocId}/{taskName}/status", async ( string allocId, string taskName, [FromServices] ManagementService managementService ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			var isAppPoolRunning = await taskHandle.IsAppPoolRunning();

			return Results.Json( new TaskStatusResponse()
			{
				AllocId = allocId,
				TaskStatus = isAppPoolRunning ? TaskStatus.Running : TaskStatus.Paused
			} );
		} ).Produces<TaskStatusResponse>();

		allocsApi.MapPut( "{allocId}/{taskName}/start", async ( string allocId, string taskName, [FromServices] ManagementService managementService ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			await taskHandle.StartAppPoolAsync();

			return Results.Ok();
		} );
		allocsApi.MapPut( "{allocId}/{taskName}/stop", async ( string allocId, string taskName, [FromServices] ManagementService managementService ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			await taskHandle.StopAppPoolAsync();

			return Results.Ok();
		} );
		allocsApi.MapPut( "{allocId}/{taskName}/recycle", async ( string allocId, string taskName, [FromServices] ManagementService managementService ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			await taskHandle.RecycleAppPoolAsync();

			return Results.Ok();
		} );

		allocsApi.MapGet( "{allocId}/{taskName}/fs/{path}",
			async ( string allocId, string taskName, string path, HttpContext context,
			[FromServices] ManagementService managementService ) =>
			{
				var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

				if ( taskHandle is null )
					return Results.NotFound();

				path = HttpUtility.UrlDecode( path );

				await taskHandle.DownloadFileAsync( context.Response, path );

				return Results.Ok();
			} ).Produces<object>( 200, "application/zip", "application/octet-stream" );

		allocsApi.MapPut( "{allocId}/{taskName}/fs/{path}",
			async ( string allocId, string taskName, string path, HttpContext context,
			[FromServices] ManagementService managementService, [FromQuery] bool clean = false ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			path = HttpUtility.UrlDecode( path );

			var isZip = context.Request.ContentType == "application/zip";

			await taskHandle.UploadFileAsync( context.Request.Body, isZip, path, false, clean );

			return Results.Ok();
		} ).Accepts<object>( "application/zip", "application/octet-stream" );
		
		allocsApi.MapPatch( "{allocId}/{taskName}/fs/{path}",
			async ( string allocId, string taskName, string path, HttpContext context,
			[FromServices] ManagementService managementService, [FromQuery] bool clean = false ) =>
			{
				var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

				if ( taskHandle is null )
					return Results.NotFound();

				path = HttpUtility.UrlDecode( path );

				var isZip = context.Request.ContentType == "application/zip";

				await taskHandle.UploadFileAsync( context.Request.Body, isZip, path, true, clean );

				return Results.Ok();
			} ).Accepts<object>( "application/zip", "application/octet-stream" );

		allocsApi.MapGet( "{allocId}/{taskName}/screenshot", async ( string allocId, string taskName, [FromServices] ManagementService managementService, [FromQuery] string path = "/" ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			var screenshot = await taskHandle.TakeScreenshotAsync( path );

			if ( screenshot is null )
				return Results.NotFound();

			return Results.Bytes( screenshot, "image/png", $"screenshot_{allocId}_{taskName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.png" );
		} );

		allocsApi.MapGet( "{allocId}/{taskName}/procdump", async (
			string allocId, string taskName, HttpContext httpContext, CancellationToken cancellationToken,
			[FromServices] ManagementService managementService ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			var dumpFile = new FileInfo( Path.GetTempFileName() + ".dmp" );

			try
			{
				await taskHandle.TakeProcessDump( dumpFile, cancellationToken );

				// Stream the file to the client
				await Results
					.File( dumpFile.FullName, "application/octet-stream", $"procdump_{allocId}_{taskName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.dmp" )
					.ExecuteAsync( httpContext );

				// Not needed but we need to return something
				return Results.Ok();
			}
			finally
			{
				if ( dumpFile.Exists )
					dumpFile.Delete();
			}
		} );

		return api;
	}

	private class TaskStatusResponse
	{
		[JsonPropertyName( "allocId" )]
		public string AllocId { get; set; } = default!;
		[JsonPropertyName( "taskStatus" )]
		public TaskStatus TaskStatus { get; set; } = default!;
	}

	[JsonConverter( typeof( JsonStringEnumConverter<TaskStatus> ) )]
	private enum TaskStatus
	{
		Paused,
		Running
	}
}
#endif
