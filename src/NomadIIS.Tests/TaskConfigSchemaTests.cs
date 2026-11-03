using NomadIIS.Services.Configuration;
using System.Linq;

namespace NomadIIS.Tests;

public class TaskConfigSchemaTests
{
	/// <summary>
	/// The schema is generated from the configuration classes by reflection and handed to nomad
	/// on startup, so an unsupported property type would take the whole driver down rather than
	/// just one job.
	/// </summary>
	[Fact]
	public void TheTaskConfigSchemaCanBeGenerated ()
	{
		var spec = HclSpecGenerator.Generate<DriverTaskConfig>();

		Assert.NotNull( spec.Object );
		Assert.Contains( "logging", spec.Object.Attributes.Keys );

		var logging = spec.Object.Attributes["logging"].BlockList;

		Assert.Equal( "logging", logging.Name );
		Assert.Equal( 1ul, logging.MaxItems );

		Assert.Equal(
			["aspnetcore_stdout", "enabled", "file", "max_bytes_per_second", "poll_interval"],
			logging.Nested.Object.Attributes.Keys.OrderBy( x => x ) );
	}

	[Fact]
	public void TheDriverConfigSchemaCanBeGenerated ()
	{
		var spec = HclSpecGenerator.Generate<DriverConfig>();

		Assert.NotNull( spec.Object );
	}
}
