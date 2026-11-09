namespace AspNetCoreSampleApp.Models;

public class HomeModel
{
	public string? Path { get; set; }
	public string? PermissionCheckResult { get; set; }
	public bool PermissionCheckFailed { get; set; }
	public string? LogMessage { get; set; }
	public string? LogResult { get; set; }
	public bool LogFailed { get; set; }
	public IDictionary<string, string?> EnvironmentVariables { get; set; } = new Dictionary<string, string?>();
	public string? EnvironmentVariablesError { get; set; }
}
