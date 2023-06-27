using MessagePack;
using System;

namespace NomadIIS.Services;

[MessagePackObject]
public struct IisTaskHandleState
{
	[Key( 0 )]
	public string AppPoolName { get; set; }
	[Key( 1 )]
	public string WebsiteName { get; set; }
	[Key( 2 )]
	public DateTime StartDate { get; set; }
}
