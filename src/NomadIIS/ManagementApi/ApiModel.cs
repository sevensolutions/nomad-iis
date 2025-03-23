﻿#if MANAGEMENT_API
using System.Text.Json.Serialization;

namespace NomadIIS.ManagementApi.ApiModel;

public sealed class TaskStatusResponse
{
	[JsonPropertyName( "allocId" )]
	public string AllocId { get; set; } = default!;
	[JsonPropertyName( "taskName" )]
	public string TaskName { get; set; } = default!;
	[JsonPropertyName( "applicationPool" )]
	public ApplicationPool ApplicationPool { get; set; } = default!;
}
public sealed class ApplicationPool
{
	[JsonPropertyName( "status" )]
	public ApplicationPoolStatus Status { get; set; }
	[JsonPropertyName( "isWorkerProcessRunning" )]
	public bool IsWorkerProcessRunning { get; set; }
}

[JsonConverter( typeof( JsonStringEnumConverter<ApplicationPoolStatus> ) )]
public enum ApplicationPoolStatus
{
	Starting,
	Started,
	Stopping,
	Stopped,
	Unknown
}

public sealed class DebugInformation
{
	[JsonPropertyName( "iisHandleCount" )]
	public int IisHandleCount { get; set; }

	[JsonPropertyName( "iisHandles" )]
	public DebugIisHandle[] IisHandles { get; set; } = default!;
}
public sealed class DebugIisHandle
{
	[JsonPropertyName( "taskId" )]
	public string TaskId { get; set; } = default!;
	[JsonPropertyName( "appPoolName" )]
	public string? AppPoolName { get; set; }
	[JsonPropertyName( "allocId" )]
	public string? AllocId { get; set; }
	[JsonPropertyName( "namespace" )]
	public string? Namespace { get; set; }
	[JsonPropertyName( "jobId" )]
	public string? JobId { get; set; }
	[JsonPropertyName( "jobName" )]
	public string? JobName { get; set; }
	[JsonPropertyName( "taskName" )]
	public string? TaskName { get; set; }
	[JsonPropertyName( "taskGroupName" )]
	public string? TaskGroupName { get; set; }
}
#endif
