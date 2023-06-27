using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugin;
using System.Threading.Tasks;

namespace NomadIIS.Services.Grpc;

public sealed class BrokerService : GRPCBroker.GRPCBrokerBase
{
	private readonly ILogger<BrokerService> _logger;

	public BrokerService ( ILogger<BrokerService> logger )
	{
		_logger = logger;
	}

	public override async Task StartStream ( IAsyncStreamReader<ConnInfo> requestStream, IServerStreamWriter<ConnInfo> responseStream, ServerCallContext context )
	{
		_logger.LogDebug( nameof( StartStream ) );

		var tcs = new TaskCompletionSource();

		context.CancellationToken.Register( () => tcs.SetResult() );

		await tcs.Task;
	}
}
