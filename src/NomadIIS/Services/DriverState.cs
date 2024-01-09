using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NomadIIS.Services
{
	public sealed class DriverState
	{
		[JsonPropertyName( "allocations" )]
		public DriverStateAlloc[]? Allocations { get; set; }
	}

	public sealed class DriverStateAlloc
	{
		[JsonPropertyName( "taskId" )]
		public string TaskId { get; set; } = default!;

		[JsonPropertyName( "allocId" )]
		public string AllocId { get; set; } = default!;

		[JsonPropertyName( "startDate" )]
		public DateTime StartDate { get; set; } = default!;

		[JsonPropertyName( "appPoolName" )]
		public string AppPoolName { get; set; } = default!;

		[JsonPropertyName( "websiteName" )]
		public string WebsiteName { get; set; } = default!;

		[JsonPropertyName( "taskOwnsWebsite" )]
		public bool TaskOwnsWebsite { get; set; } = default!;

		[JsonPropertyName( "applicationAliases" )]
		public string[]? ApplicationAliases { get; set; }

		public override string ToString ()
			=> $"Website: {WebsiteName}, AppPool: {AppPoolName}, StartDate: {StartDate.ToLocalTime()}";
	}



	// Note: MessagePack, do not re-order!

	[MessagePackObject]
	public class DriverStateV1
	{
		[IgnoreMember]
		public int Version => 1;

		[Key( 0 )]
		public DateTime StartDate { get; set; } = default!;

		[Key( 1 )]
		public string AppPoolName { get; set; } = default!;

		[Key( 2 )]
		public string WebsiteName { get; set; } = default!;

		[Key( 3 )]
		public bool TaskOwnsWebsite { get; set; } = default!;

		[Key( 4 )]
		public List<string?> ApplicationAliases { get; set; } = default!;

		public override string ToString ()
			=> $"Website: {WebsiteName}, AppPool: {AppPoolName}, StartDate: {StartDate.ToLocalTime()}";
	}
}
