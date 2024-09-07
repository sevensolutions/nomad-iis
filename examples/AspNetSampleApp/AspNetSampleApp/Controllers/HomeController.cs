using AspNetSampleApp.Models;
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
		public ActionResult Index ()
		{
			return View( PopulateEnvironmentVariables( new HomeModel() ) );
		}

		[HttpPost]
		public ActionResult CheckPermissions ( string path, string submitButton )
		{
			var result = "";

			if ( !string.IsNullOrEmpty( path ) )
			{
				try
				{
					result = ExecuteAction( submitButton, path );
				}
				catch ( Exception ex )
				{
					result = $"💣 {ex.Message}";
				}
			}
			else
				result = "No path specified!";

			return View( "Index", PopulateEnvironmentVariables( new HomeModel()
			{
				PermissionCheckResult = result
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

		private static HomeModel PopulateEnvironmentVariables ( HomeModel model )
		{
			try
			{
				var sb = new StringBuilder();

				foreach ( DictionaryEntry v in Environment.GetEnvironmentVariables() )
					sb.AppendLine( $"{v.Key} = {v.Value}" );

				model.EnvironmentVariables = sb.ToString();
			}
			catch ( Exception ex )
			{
				model.EnvironmentVariables = $"💣 {ex.Message}";
			}

			return model;
		}
	}
}
