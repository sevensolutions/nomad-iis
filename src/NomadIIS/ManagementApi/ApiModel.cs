using System.Text.Json.Serialization;

namespace NomadIIS.ManagementApi.ApiModel;

public class TaskStatusResponse
{
	[JsonPropertyName( "allocId" )]
	public string AllocId { get; set; } = default!;
	[JsonPropertyName( "taskName" )]
	public string TaskName { get; set; } = default!;
	[JsonPropertyName( "applicationPool" )]
	public ApplicationPool ApplicationPool { get; set; } = default!;
}
public class ApplicationPool
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
