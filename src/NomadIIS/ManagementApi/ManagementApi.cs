#if MANAGEMENT_API
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NomadIIS.Services;
using System;
using System.Text.Json.Serialization;

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

		allocsApi.MapPut( "{allocId}/{taskName}/upload", async ( string allocId, string taskName, HttpContext context, [FromServices] ManagementService managementService, [FromQuery] string appAlias = "/" ) =>
		{
			if ( !string.IsNullOrEmpty( appAlias ) && !appAlias.StartsWith( '/' ) )
				appAlias = $"/{appAlias}";

			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			await taskHandle.UploadAsync( context.Request.Body, appAlias );

			return Results.Ok();
		} ).Accepts<object>( "application/zip" );

		allocsApi.MapGet( "{allocId}/{taskName}/screenshot", async ( string allocId, string taskName, [FromServices] ManagementService managementService, [FromQuery] string path = "/" ) =>
		{
			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			var screenshot = await taskHandle.TakeScreenshotAsync( path );

			if ( screenshot is null )
				return Results.NotFound();

			return Results.Bytes( screenshot, "image/png" );
		} );

		allocsApi.MapGet( "{allocId}/{taskName}/procdump", async ( string allocId, string taskName, HttpContext httpContext, [FromServices] IConfiguration configuration, [FromServices] ManagementService managementService ) =>
		{
			var eulaAccepted = configuration.GetValue( "procdump-accept-eula", false );
			if ( !eulaAccepted )
				throw new InvalidOperationException( "Procdump EULA has not been accepted." );

			var taskHandle = managementService.TryGetHandleByAllocIdAndTaskName( allocId, taskName );

			if ( taskHandle is null )
				return Results.NotFound();

			var dumpFile = await taskHandle.TakeProcessDump();

			try
			{
				// Stream the file to the client
				await Results
					.File( dumpFile.FullName, "application/octet-stream", $"procdump_{allocId}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.dmp" )
					.ExecuteAsync( httpContext );

				// Not needed but we need to return something
				return Results.Ok();
			}
			finally
			{
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
