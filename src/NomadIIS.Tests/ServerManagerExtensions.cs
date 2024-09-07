using Microsoft.Web.Administration;
using System.Linq;

namespace NomadIIS.IntegrationTests;

public static class ServerManagerExtensions
{
	public static bool ApplicationPoolExists ( this ServerManager serverManager, string name )
		=> serverManager.ApplicationPools.Any( x => x.Name == name );
	public static bool WebsiteExists ( this ServerManager serverManager, string name )
		=> serverManager.Sites.Any( x => x.Name == name );

	public static string? GetApplicationPoolEnvironmentVariable ( this ServerManager serverManager, string appPoolName, string envName )
	{
		var appPool = serverManager.ApplicationPools.First( x => x.Name == appPoolName );

		var envVarsCollection = appPool.GetCollection( "environmentVariables" );

		foreach(var envVar in envVarsCollection )
		{
			var name = envVar.GetAttributeValue( "name" ).ToString();
			if ( name == envName )
				return envVar.GetAttributeValue( "value" )?.ToString();
		}

		return null;
	}
}
