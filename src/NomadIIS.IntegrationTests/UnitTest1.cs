using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NomadIIS.IntegrationTests;

public class UnitTest1 : IClassFixture<NomadIISFixture>
{
	private readonly NomadIISFixture _fixture;

	public UnitTest1 ( NomadIISFixture fixture )
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Test1 ()
	{
		Console.WriteLine( "Waiting for nomad to start..." );

		var jobHcl = File.ReadAllText( @"Data\simple-job.hcl" );

		var jobId = await _fixture.ScheduleJobAsync( jobHcl );

		var allocations = await _fixture.ListJobAllocationsAsync( jobId );

		if ( allocations is null || allocations.Length == 0 )
			Assert.Fail( "No job allocations" );

		_fixture.AssertApplicationPool( $"nomad-{allocations[0].Id}-iis-simple" );
		_fixture.AssertWebsite( $"nomad-{allocations[0].Id}-iis-simple" );

		await Task.Delay( 2000 );

		await _fixture.StopJobAsync( jobId );
	}
}
