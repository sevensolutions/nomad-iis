using MessagePack;
using System;
using System.Collections.Generic;

namespace NomadIIS.Services;

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

	[Key( 5 )]
	public int? UdpLoggerPort { get; set; }

	public override string ToString ()
		=> $"Website: {WebsiteName}, AppPool: {AppPoolName}, StartDate: {StartDate.ToLocalTime()}";
}

[MessagePackObject]
public class DriverStateV2
{
	[IgnoreMember]
	public int Version => 2;

	[Key( 0 )]
	public DateTime StartDate { get; set; } = default!;

	/// <summary>
	/// Key = Alias, Value = Full App Pool Name
	/// </summary>
	[Key( 1 )]
	public Dictionary<string, string> AppPoolNames { get; set; } = default!;

	[Key( 2 )]
	public string WebsiteName { get; set; } = default!;

	[Key( 3 )]
	public bool TaskOwnsWebsite { get; set; } = default!;

	[Key( 4 )]
	public List<string?> ApplicationAliases { get; set; } = default!;

	[Key( 5 )]
	public int? UdpLoggerPort { get; set; }

	[Key( 6 )]
	public List<string>? ServiceAutoStartProviderNames { get; set; }

	/// <summary>
	/// The resolved log shipping settings. These have to live in the driver state because
	/// MsgpackDriverConfig is empty when nomad asks us to recover a task, so the task
	/// configuration is not available at that point.
	/// </summary>
	[Key( 7 )]
	public LogShippingState? LogShipping { get; set; }

	public override string ToString ()
		=> $"Website: {WebsiteName}, StartDate: {StartDate.ToLocalTime()}";
}

[MessagePackObject]
public class LogShippingState
{
	/// <summary>
	/// Absolute directory the pattern is resolved against.
	/// </summary>
	[Key( 0 )]
	public string RootDirectory { get; set; } = default!;

	/// <summary>
	/// File name pattern, relative to <see cref="RootDirectory"/>, may contain a wildcard.
	/// </summary>
	[Key( 1 )]
	public string Pattern { get; set; } = default!;

	[Key( 2 )]
	public TimeSpan PollInterval { get; set; }

	[Key( 3 )]
	public long MaxBytesPerSecond { get; set; }
}
