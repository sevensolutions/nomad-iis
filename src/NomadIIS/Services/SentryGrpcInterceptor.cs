using Grpc.Core;
using Grpc.Core.Interceptors;
using Sentry;
using System;
using System.Threading.Tasks;

namespace NomadIIS.Services;

public class SentryGrpcInterceptor : Interceptor
{
	public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse> ( TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation )
	{
		try
		{
			return await continuation( request, context );
		}
		catch ( Exception ex )
		{
			SentrySdk.CaptureException( ex );
			throw;
		}
	}
}
