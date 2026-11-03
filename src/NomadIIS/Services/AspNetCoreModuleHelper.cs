using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NomadIIS.Services;

/// <summary>
/// Helpers for the ASP.NET Core Module (ANCM).
/// </summary>
internal static class AspNetCoreModuleHelper
{
	/// <summary>
	/// Directory below the task's local directory where we let ANCM write the stdout logs.
	/// The local directory is used because <see cref="IisTaskHandle"/> already grants the
	/// application pool identity full control over it.
	/// </summary>
	public const string StdoutLogDirectory = @"local\nomad-iis-logs";

	/// <summary>
	/// ANCM appends <c>_&lt;pid&gt;_&lt;timestamp&gt;.log</c> to the configured file name.
	/// </summary>
	public const string StdoutLogFilePrefix = "stdout";

	/// <summary>
	/// The pattern, relative to the task directory, which matches the files ANCM produces.
	/// </summary>
	public const string StdoutLogPattern = $@"{StdoutLogDirectory}\{StdoutLogFilePrefix}_*.log";

	/// <summary>
	/// Turns on ANCM stdout logging in the application's web.config so that the real stdout of
	/// the .NET process ends up in a file we can tail.
	/// </summary>
	/// <remarks>
	/// Only applications which already have an <c>aspNetCore</c> element are touched. Adding
	/// that element to an application which is not hosted by ANCM would break it, so a missing
	/// element is reported back to the caller instead.
	/// </remarks>
	/// <returns>True if at least one <c>aspNetCore</c> element was patched.</returns>
	public static bool TryEnableStdoutLogging ( string applicationPhysicalPath, string absoluteLogFilePrefix, ILogger logger )
	{
		var webConfigPath = Path.Combine( applicationPhysicalPath, "web.config" );

		if ( !File.Exists( webConfigPath ) )
			return false;

		try
		{
			var document = XDocument.Load( webConfigPath, LoadOptions.PreserveWhitespace );

			// An aspNetCore element may also live inside a <location> block, so search the
			// whole document rather than just the well-known path.
			var elements = document.Descendants( "aspNetCore" ).ToArray();

			if ( elements.Length == 0 )
				return false;

			foreach ( var element in elements )
			{
				element.SetAttributeValue( "stdoutLogEnabled", "true" );
				element.SetAttributeValue( "stdoutLogFile", absoluteLogFilePrefix );
			}

			document.Save( webConfigPath, SaveOptions.DisableFormatting );

			return true;
		}
		catch ( Exception ex )
		{
			logger.LogWarning( ex, $"Failed to enable ANCM stdout logging in {webConfigPath}." );

			return false;
		}
	}
}
