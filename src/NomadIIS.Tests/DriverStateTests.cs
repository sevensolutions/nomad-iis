using MessagePack;
using NomadIIS.Services;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace NomadIIS.Tests;

public class DriverStateTests
{
	[Fact]
	public void LogShippingSettingsSurviveARoundTrip ()
	{
		var state = new DriverStateV2()
		{
			StartDate = new DateTime( 2026, 11, 3, 10, 0, 0, DateTimeKind.Utc ),
			AppPoolNames = new Dictionary<string, string>() { ["default"] = "nomad-abc-task" },
			WebsiteName = "nomad-abc-task",
			TaskOwnsWebsite = true,
			ApplicationAliases = new List<string?>() { null },
			LogShipping = new LogShippingState()
			{
				RootDirectory = @"C:\nomad\alloc\abc\task",
				Pattern = @"local\nomad-iis-logs\stdout_*.log",
				PollInterval = TimeSpan.FromSeconds( 1 ),
				MaxBytesPerSecond = 1048576
			}
		};

		var restored = MessagePackSerializer.Deserialize<DriverStateV2>(
			MessagePackSerializer.Serialize( state ) );

		Assert.NotNull( restored.LogShipping );
		Assert.Equal( state.LogShipping.RootDirectory, restored.LogShipping!.RootDirectory );
		Assert.Equal( state.LogShipping.Pattern, restored.LogShipping.Pattern );
		Assert.Equal( state.LogShipping.PollInterval, restored.LogShipping.PollInterval );
		Assert.Equal( state.LogShipping.MaxBytesPerSecond, restored.LogShipping.MaxBytesPerSecond );
	}

	/// <summary>
	/// A driver which is upgraded while tasks are running has to be able to recover state that
	/// was written before the log shipping field existed, so it must tolerate a state array
	/// that is one element short.
	/// </summary>
	[Fact]
	public void StateWrittenBeforeLogShippingExistedCanStillBeRecovered ()
	{
		var buffer = new ArrayBufferWriter<byte>();
		var writer = new MessagePackWriter( buffer );

		// The seven fields DriverStateV2 had before LogShipping was added at Key(7).
		writer.WriteArrayHeader( 7 );
		writer.Write( new DateTime( 2026, 11, 3, 10, 0, 0, DateTimeKind.Utc ) );
		writer.WriteMapHeader( 1 );
		writer.Write( "default" );
		writer.Write( "nomad-abc-task" );
		writer.Write( "nomad-abc-task" );
		writer.Write( true );
		writer.WriteArrayHeader( 1 );
		writer.WriteNil();
		writer.WriteNil();
		writer.WriteNil();
		writer.Flush();

		var restored = MessagePackSerializer.Deserialize<DriverStateV2>( buffer.WrittenMemory );

		Assert.Equal( "nomad-abc-task", restored.WebsiteName );
		Assert.True( restored.TaskOwnsWebsite );
		Assert.Equal( "nomad-abc-task", restored.AppPoolNames["default"] );

		// No settings means the shipper simply stays off until the task is restarted.
		Assert.Null( restored.LogShipping );
	}
}
