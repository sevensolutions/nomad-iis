#if MANAGEMENT_API
using System.Text.Json.Serialization;

namespace NomadIIS.ManagementApi.ApiModel;

public sealed class TaskStatusResponse
{
	[JsonPropertyName( "allocId" )]
	public string AllocId { get; set; } = default!;
	[JsonPropertyName( "taskName" )]
	public string TaskName { get; set; } = default!;
	[JsonPropertyName( "defaultApplicationPool" )]
	public ApplicationPool? DefaultApplicationPool { get; set; } = default!;
	[JsonPropertyName( "applicationPools" )]
	public ApplicationPool[] ApplicationPools { get; set; } = default!;
}
public sealed class ApplicationPool
{
	[JsonPropertyName( "name" )]
	public string Name { get; set; } = default!;
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

	[JsonPropertyName( "danglingIisAppPools" )]
	public int DanglingIisAppPools { get; set; }
	[JsonPropertyName( "danglingIisWebsites" )]
	public int DanglingIisWebsites { get; set; }

	[JsonPropertyName( "iisAppPools" )]
	public DebugIisAppPool[] IisAppPools { get; set; } = default!;

	[JsonPropertyName( "iisWebsites" )]
	public DebugIisWebsite[] IisWebsites { get; set; } = default!;
}

public sealed class DebugIisHandle
{
	[JsonPropertyName( "taskId" )]
	public string TaskId { get; set; } = default!;
	[JsonPropertyName( "appPoolNames" )]
	public string[]? AppPoolNames { get; set; }
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
	[JsonPropertyName( "isRecovered" )]
	public bool IsRecovered { get; set; }
}

public sealed class DebugIisAppPool
{
	[JsonPropertyName( "name" )]
	public string Name { get; set; } = default!;

	[JsonPropertyName( "isDangling" )]
	public bool IsDangling { get; set; }
}

public sealed class DebugIisWebsite
{
	[JsonPropertyName( "name" )]
	public string Name { get; set; } = default!;

	[JsonPropertyName( "isDangling" )]
	public bool IsDangling { get; set; }
}
#endif
