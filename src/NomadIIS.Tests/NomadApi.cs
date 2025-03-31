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

public sealed class AllocationResponse
{
	[JsonPropertyName( "ID" )]
	public string Id { get; set; } = default!;
	[JsonPropertyName( "Name" )]
	public string Name { get; set; } = default!;
	[JsonPropertyName( "Resources" )]
	public AllocationResources Resources { get; set; } = default!;
}
public sealed class AllocationResources
{
	[JsonPropertyName( "Networks" )]
	public AllocationNetwork[] Networks { get; set; } = default!;
}
public sealed class AllocationNetwork
{
	[JsonPropertyName( "DynamicPorts" )]
	public NetworkDynamicPort[] DynamicPorts { get; set; } = default!;
}
public sealed class NetworkDynamicPort
{
	[JsonPropertyName( "Label" )]
	public string Label { get; set; } = default!;
	[JsonPropertyName( "Value" )]
	public int Value { get; set; } = default!;
	[JsonPropertyName( "To" )]
	public int To { get; set; } = default!;
	[JsonPropertyName( "HostNetwork" )]
	public string HostNetwork { get; set; } = default!;
}
