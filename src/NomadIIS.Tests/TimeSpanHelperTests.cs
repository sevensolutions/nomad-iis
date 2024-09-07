using NomadIIS.Services;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NomadIIS.Tests;

public class TimeSpanHelperTests
{
	[Theory]
	[ClassData( typeof( TestData ) )]
	public void Run ( string value, TimeSpan expectedTimeSpan )
	{
		var timespan = TimeSpanHelper.Parse( value );
		Assert.Equal( expectedTimeSpan, timespan );
	}

	private class TestData : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator ()
		{
			yield return new object[] { "1w", TimeSpan.FromDays( 7 ) };
			yield return new object[] { "3d", TimeSpan.FromDays( 3 ) };
			yield return new object[] { "0h", TimeSpan.Zero };
			yield return new object[] { "1h", TimeSpan.FromHours( 1 ) };
			yield return new object[] { "42m", TimeSpan.FromMinutes( 42 ) };
			yield return new object[] { "42s", TimeSpan.FromSeconds( 42 ) };
			yield return new object[] { "741ms", TimeSpan.FromMilliseconds( 741 ) };
			yield return new object[] { "7h2m", new TimeSpan( 7, 2, 0 ) };
			yield return new object[] { "7h2m3s", new TimeSpan( 7, 2, 3 ) };

			yield return new object[] { "00:00", TimeSpan.Zero };
			yield return new object[] { "04:51", new TimeSpan( 4, 51, 0 ) };
			yield return new object[] { "04:51:23", new TimeSpan( 4, 51, 23 ) };
			yield return new object[] { "04:51:23.740", new TimeSpan( 0, 4, 51, 23, 740 ) };
			yield return new object[] { "07.04:51:23", new TimeSpan( 7, 4, 51, 23 ) };
		}

		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator();
	}
}
