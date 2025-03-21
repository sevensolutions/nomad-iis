#if MANAGEMENT_API
using Microsoft.AspNetCore.Http;
using NomadIIS.Services;
using System;

namespace NomadIIS.ManagementApi;

public static class Authorization
{
	public static bool IsAuthorized ( this IisTaskHandle taskHandle, HttpContext httpContext )
	{
		var jobNamespace = taskHandle.TaskConfig?.Namespace ?? throw new InvalidOperationException();
		var jobName = taskHandle.TaskConfig?.JobName ?? throw new InvalidOperationException();
		var allocId = taskHandle.TaskConfig?.AllocId ?? throw new InvalidOperationException();

		if ( !httpContext.User.HasClaim( "namespace", jobNamespace ) && !httpContext.User.HasClaim( "namespace", "*" ) )
			return false;
		if ( !httpContext.User.HasClaim( "job", jobName ) && !httpContext.User.HasClaim( "job", "*" ) )
			return false;
		if ( !httpContext.User.HasClaim( "allocId", allocId ) && !httpContext.User.HasClaim( "allocId", "*" ) )
			return false;

		return true;
	}
}
#endif
