using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NomadIIS.Services;
using System.Text.Json.Serialization;

namespace NomadIIS
{
	public sealed class ManagementApi
	{
		public static IEndpointConventionBuilder Map ( WebApplication app )
		{
			var api = app.MapGroup( "/api/v1" );

			var jobsApi = api.MapGroup( "/jobs" );

			jobsApi.MapGet( "{namespaceName}/{jobName}/status", async ( string namespaceName, string jobName, [FromServices] ManagementService managementService ) =>
			{
				var taskHandle = managementService.TryGetHandleByJobName( namespaceName, jobName );

				if ( taskHandle is null )
					return Results.NotFound();

				var isAppPoolRunning = await taskHandle.IsAppPoolRunning();

				return Results.Json( new TaskStatusResponse()
				{
					Namespace = namespaceName,
					JobName = jobName,
					TaskStatus = isAppPoolRunning ? TaskStatus.Running : TaskStatus.Paused
				} );
			} );

			// TODO: Should we put namespcae to a query-param, like on the Nomad REST API?
			jobsApi.MapPut( "{namespaceName}/{jobName}/upload", async ( string namespaceName, string jobName, HttpContext context, [FromServices] ManagementService managementService, [FromQuery] string appAlias = "/" ) =>
			{
				if ( !string.IsNullOrEmpty( appAlias ) && !appAlias.StartsWith( '/' ) )
					appAlias = $"/{appAlias}";

				var taskHandle = managementService.TryGetHandleByJobName( namespaceName, jobName );

				if ( taskHandle is null )
					return Results.NotFound();

				await taskHandle.UploadAsync( context.Request.Body, appAlias );

				return Results.Ok();
			} );

			jobsApi.MapGet( "{namespaceName}/{jobName}/screenshot", async ( string namespaceName, string jobName, [FromServices] ManagementService managementService, [FromQuery] string appAlias = "/" ) =>
			{
				if ( !string.IsNullOrEmpty( appAlias ) && !appAlias.StartsWith( '/' ) )
					appAlias = $"/{appAlias}";

				var taskHandle = managementService.TryGetHandleByJobName( namespaceName, jobName );

				if ( taskHandle is null )
					return Results.NotFound();

				var screenshot = await taskHandle.TakeScreenshotAsync( appAlias );

				if ( screenshot is null )
					return Results.NotFound();

				return Results.Bytes( screenshot, "image/png" );
			} );

			return api;
		}

		private class TaskStatusResponse
		{
			[JsonPropertyName( "namespace" )]
			public string Namespace { get; set; } = default!;
			[JsonPropertyName( "jobName" )]
			public string JobName { get; set; } = default!;
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
}
