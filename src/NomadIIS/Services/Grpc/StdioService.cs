using Grpc.Core;
using Microsoft.Extensions.Logging;
using Plugin;
using System.Threading.Tasks;

namespace NomadIIS.Services.Grpc;

public sealed class StdioService : GRPCStdio.GRPCStdioBase
{
	private readonly ILogger<StdioService> _logger;

	public StdioService ( ILogger<StdioService> logger )
	{
		_logger = logger;
	}

	public override async Task StreamStdio ( Google.Protobuf.WellKnownTypes.Empty request, IServerStreamWriter<StdioData> responseStream, ServerCallContext context )
	{
		_logger.LogInformation( nameof( StreamStdio ) );

		var tcs = new TaskCompletionSource();

		context.CancellationToken.Register( () => tcs.SetResult() );

		await tcs.Task;
	}
}
