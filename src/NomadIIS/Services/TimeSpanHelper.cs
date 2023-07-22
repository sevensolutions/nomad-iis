using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NomadIIS.Services
{
	internal static class TimeSpanHelper
	{
		private static readonly Regex _timeSpanRegex = new(
			@"^(?:(?<weeks>\d+)(?:w))?\s*(?:(?<days>\d+)(?:d))?\s*(?:(?<hours>\d+)(?:h))?\s*(?:(?<minutes>\d+)(?:m))?\s*(?:(?<seconds>\d+)(?:s))?\s*(?:(?<milliseconds>\d+)(?:ms))?$" );

		public static TimeSpan? TryParse ( string value )
		{
			if ( string.IsNullOrWhiteSpace( value ) )
				return null;

			var match = _timeSpanRegex.Match( value );

			if ( match.Success )
			{
				if ( match.Groups["weeks"]?.Value is not string strWeeks || !int.TryParse( strWeeks, out var weeks ) )
					weeks = 0;
				if ( match.Groups["days"]?.Value is not string strDays || !int.TryParse( strDays, out var days ) )
					days = 0;
				if ( match.Groups["hours"]?.Value is not string strHours || !int.TryParse( strHours, out var hours ) )
					hours = 0;
				if ( match.Groups["minutes"]?.Value is not string strMinutes || !int.TryParse( strMinutes, out var minutes ) )
					minutes = 0;
				if ( match.Groups["seconds"]?.Value is not string strSeconds || !int.TryParse( strSeconds, out var seconds ) )
					seconds = 0;
				if ( match.Groups["milliseconds"]?.Value is not string strMilliseconds || !int.TryParse( strMilliseconds, out var milliseconds ) )
					milliseconds = 0;

				return new TimeSpan( ( weeks * 7 ) + days, hours, minutes, seconds, milliseconds );
			}

			if ( TimeSpan.TryParseExact( value, "c", CultureInfo.InvariantCulture, out var ts ) )
				return ts;

			return null;
		}
		public static bool TryParse ( string value, out TimeSpan? timeSpan )
		{
			timeSpan = TryParse( value );

			return timeSpan is not null;
		}
		public static TimeSpan Parse ( string value )
			=> TryParse( value ) ?? throw new FormatException( $"Invalid time span value {value}." );
	}
}
