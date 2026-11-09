using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Web.Hosting;

namespace AspNetSampleApp
{
	public class LoggingConfig
	{
		public static void Configure ()
		{
			// Under Nomad IIS the app pool is granted write access to the allocation's log directory,
			// but not necessarily to the site directory.
			var allocationDirectory = Environment.GetEnvironmentVariable( "NOMAD_ALLOC_DIR" );

			// Referenced by the file name in log4net.config, so it needs to be set before configuring.
			GlobalContext.Properties["LogDirectory"] = string.IsNullOrEmpty( allocationDirectory )
				? HostingEnvironment.MapPath( "~/App_Data/logs" )
				: Path.Combine( allocationDirectory, "logs" );

			XmlConfigurator.ConfigureAndWatch( new FileInfo( HostingEnvironment.MapPath( "~/log4net.config" ) ) );
		}
	}
}
