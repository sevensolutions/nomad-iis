#if MANAGEMENT_API
using Microsoft.AspNetCore.Http;
using NomadIIS.Services;
using System;

namespace NomadIIS.ManagementApi;

public static class Authorization
{
	public static bool IsAuthorized ( this IisTaskHandle taskHandle, HttpContext httpContext, AuthorizationCapability capability )
	{
		var jobNamespace = taskHandle.TaskConfig?.Namespace ?? throw new InvalidOperationException();
		var jobName = taskHandle.TaskConfig?.JobName ?? throw new InvalidOperationException();
		var allocId = taskHandle.TaskConfig?.AllocId ?? throw new InvalidOperationException();


		if ( !httpContext.User.HasClaim( "namespace", jobName ) && !httpContext.User.HasClaim( "namespace", "*" ) )
			return false;
		if ( !httpContext.User.HasClaim( "jobName", jobName ) && !httpContext.User.HasClaim( "jobName", "*" ) )
			return false;
		if ( !httpContext.User.HasClaim( "allocId", allocId ) && !httpContext.User.HasClaim( "allocId", "*" ) )
			return false;

		if ( !IsAuthorized( httpContext, capability ) )
			return false;

		return true;
	}
	public static bool IsAuthorized ( HttpContext httpContext, AuthorizationCapability capability )
	{
		var capabilityName = Enum.GetName( capability )!;

		if ( !httpContext.User.HasClaim( "capabilities", capabilityName ) && !httpContext.User.HasClaim( "capabilities", "*" ) )
			return false;

		return true;
	}
}

public enum AuthorizationCapability
{
	Status,
	FilesystemAccess,
	AppPoolLifecycle,
	Screenshots,
	ProcDump,
	Debug
}
#endif
