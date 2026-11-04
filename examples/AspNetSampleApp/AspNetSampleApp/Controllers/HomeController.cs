using AspNetSampleApp.Models;
using log4net;
using log4net.Appender;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace AspNetSampleApp.Controllers
{
	public class HomeController : Controller
	{
		private static readonly ILog Log = LogManager.GetLogger( typeof( HomeController ) );

		public ActionResult Index ()
		{
			return View( PopulateModel( new HomeModel() ) );
		}

		[HttpPost]
		public ActionResult CheckPermissions ( string path, string submitButton )
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

			return View( "Index", PopulateModel( new HomeModel()
			{
				Path = path,
				PermissionCheckResult = result,
				PermissionCheckFailed = failed
			} ) );
		}

		[HttpPost]
		[ValidateInput( false )] // The message is free text. It only ends up in the log and HTML-encoded in the view.
		public ActionResult WriteLog ( string message )
		{
			// Keep one message on one line, so it can't forge additional log entries.
			var singleLineMessage = message?.Replace( "\r", " " ).Replace( "\n", " " ).Trim();

			var result = "";
			var failed = false;

			if ( !string.IsNullOrEmpty( singleLineMessage ) )
			{
				// log4net doesn't report write errors to the caller, so check that the file actually changed.
				var logFilePath = GetLogFilePath();
				var lengthBefore = GetFileLength( logFilePath );

				Log.Info( singleLineMessage );

				if ( GetFileLength( logFilePath ) != lengthBefore )
				{
					result = "✔ Message written to the log!";
				}
				else
				{
					result = $"💣 The message could not be written to {logFilePath}.";
					failed = true;
				}
			}
			else
			{
				result = "No message specified!";
				failed = true;
			}

			return View( "Index", PopulateModel( new HomeModel()
			{
				LogMessage = failed ? message : null,
				LogResult = result,
				LogFailed = failed
			} ) );
		}

		private static string ExecuteAction ( string action, string path )
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

		private static string GetLogFilePath ()
		{
			return LogManager.GetRepository().GetAppenders().OfType<FileAppender>().FirstOrDefault()?.File;
		}

		private static long GetFileLength ( string path )
		{
			var file = new FileInfo( path );

			return file.Exists ? file.Length : 0;
		}

		private static HomeModel PopulateModel ( HomeModel model )
		{
			model.LogFilePath = GetLogFilePath();

			return PopulateEnvironmentVariables( model );
		}

		private static HomeModel PopulateEnvironmentVariables ( HomeModel model )
		{
			try
			{
				var variables = new SortedDictionary<string, string>( StringComparer.OrdinalIgnoreCase );

				foreach ( DictionaryEntry v in Environment.GetEnvironmentVariables() )
					variables[(string)v.Key] = (string)v.Value;

				model.EnvironmentVariables = variables;
			}
			catch ( Exception ex )
			{
				model.EnvironmentVariablesError = $"💣 {ex.Message}";
			}

			return model;
		}
	}
}
