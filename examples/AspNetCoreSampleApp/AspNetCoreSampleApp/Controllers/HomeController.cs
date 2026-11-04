using AspNetCoreSampleApp.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Text;

namespace AspNetCoreSampleApp.Controllers;

public class HomeController ( ILogger<HomeController> logger ) : Controller
{
	public IActionResult Index ()
	{
		return View( PopulateEnvironmentVariables( new HomeModel() ) );
	}

	[HttpPost]
	public IActionResult CheckPermissions ( string? path, string? submitButton )
	{
		var result = "";
		var failed = false;

		if ( !string.IsNullOrEmpty( path ) )
		{
			try
			{
				result = ExecuteAction( submitButton, path );
			}
			catch ( Exception ex )
			{
				result = $"💣 {ex.Message}";
				failed = true;
			}
		}
		else
		{
			result = "No path specified!";
			failed = true;
		}

		return View( "Index", PopulateEnvironmentVariables( new HomeModel()
		{
			Path = path,
			PermissionCheckResult = result,
			PermissionCheckFailed = failed
		} ) );
	}

	[HttpPost]
	public IActionResult WriteLog ( string? message )
	{
		// Keep one message on one line, so it can't forge additional log entries.
		var singleLineMessage = message?.Replace( "\r", " " ).Replace( "\n", " " ).Trim();

		var result = "";
		var failed = false;

		if ( !string.IsNullOrEmpty( singleLineMessage ) )
		{
			logger.LogInformation( "{Message}", singleLineMessage );

			// The logger doesn't report whether its providers managed to write the message.
			result = "✔ Message passed to the logger!";
		}
		else
		{
			result = "No message specified!";
			failed = true;
		}

		return View( "Index", PopulateEnvironmentVariables( new HomeModel()
		{
			LogMessage = failed ? message : null,
			LogResult = result,
			LogFailed = failed
		} ) );
	}

	public IActionResult Error ()
	{
		return View();
	}

	private static string ExecuteAction ( string? action, string path )
	{
		switch ( action )
		{
			case "FileExists":
				if ( System.IO.File.Exists( path ) )
					return "✔ File exists!";
				else
					return "❌ File does not exist!";
			case "ReadFile":
				return System.IO.File.ReadAllText( path );
			case "WriteFile":
				System.IO.File.WriteAllText( path, "Hello World" );
				return "✔ File written!";
			case "DeleteFile":
				System.IO.File.Delete( path );
				return "✔ File deleted!";
			case "ListDirectory":
				var sb = new StringBuilder();

				var directory = new DirectoryInfo( path );
				foreach ( var d in directory.GetDirectories() )
					sb.AppendLine( $"📁 {d.Name}" );
				foreach ( var f in directory.GetFiles() )
					sb.AppendLine( $"📄 {f.Name}" );

				if ( sb.Length == 0 )
					sb.AppendLine( "Directory is empty!" );

				return sb.ToString();

			default:
				throw new NotSupportedException();
		}
	}

	private static HomeModel PopulateEnvironmentVariables ( HomeModel model )
	{
		try
		{
			var variables = new SortedDictionary<string, string?>( StringComparer.OrdinalIgnoreCase );

			foreach ( DictionaryEntry v in Environment.GetEnvironmentVariables() )
				variables[(string)v.Key] = (string?)v.Value;

			model.EnvironmentVariables = variables;
		}
		catch ( Exception ex )
		{
			model.EnvironmentVariablesError = $"💣 {ex.Message}";
		}

		return model;
	}
}
