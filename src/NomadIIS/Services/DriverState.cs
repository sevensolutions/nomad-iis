﻿using Hashicorp.Nomad.Plugins.Drivers.Proto;
using MessagePack;
using System;
using System.Collections.Generic;

namespace NomadIIS.Services
{
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

		public override string ToString ()
			=> $"Website: {WebsiteName}, StartDate: {StartDate.ToLocalTime()}";
	}
}
