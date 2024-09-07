using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NomadIIS.Tests;

public sealed class AgentHealthResponse
{
	[JsonPropertyName( "client" )]
	public AgentHealth Client { get; set; } = default!;
	[JsonPropertyName( "server" )]
	public AgentHealth Server { get; set; } = default!;
}
public sealed class AgentHealth
{
	[JsonPropertyName( "ok" )]
	public bool Ok { get; set; }
	[JsonPropertyName( "message" )]
	public string Message { get; set; } = default!;
}

public sealed class ParseJobRequest
{
	[JsonPropertyName( "namespace" )]
	public string? Namespace { get; set; }
	[JsonPropertyName( "JobHCL" )]
	public string JobHcl { get; set; } = default!;
	[JsonPropertyName( "Canonicalize" )]
	public bool Canonicalize { get; set; }
	[JsonPropertyName( "Variables" )]
	public string? Variables { get; set; }
}
public sealed class ParseJobResponse
{
	[JsonExtensionData]
	public Dictionary<string, object> Data { get; set; } = default!;
}

public sealed class CreateJobRequest
{
	public Dictionary<string, object> Job { get; set; } = default!;
}
public sealed class CreateJobResponse
{
	[JsonPropertyName( "EvalID" )]
	public string EvalId { get; set; } = default!;
}

public sealed class JobResponse
{
	[JsonPropertyName( "ID" )]
	public string Id { get; set; } = default!;
	[JsonPropertyName( "Name" )]
	public string Name { get; set; } = default!;
	[JsonPropertyName( "Status" )]
	public JobStatus Status { get; set; }
}

public sealed class JobAllocationResponse
{
	[JsonPropertyName( "ID" )]
	public string Id { get; set; } = default!;
	[JsonPropertyName( "Name" )]
	public string Name { get; set; } = default!;
}

[JsonConverter( typeof( JsonStringEnumConverter<JobStatus> ) )]
public enum JobStatus
{
	[JsonPropertyName( "pending" )]
	Pending,
	[JsonPropertyName( "running" )]
	Running,
	[JsonPropertyName( "dead" )]
	Dead
}
