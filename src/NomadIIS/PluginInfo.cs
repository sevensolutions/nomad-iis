using System.Reflection;

namespace NomadIIS;

internal static class PluginInfo
{
	public static readonly string Name = "iis";
	public static readonly string Version = GetVersion();

	private static string GetVersion ()
	{
		var version = typeof( PluginInfo ).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion;

		if ( string.IsNullOrWhiteSpace( version ) )
		{
			version = typeof( PluginInfo ).Assembly.GetName().Version?.ToString();
		}

		return version?.Split( '+' )[0] ?? "0.0.0";
	}
}
