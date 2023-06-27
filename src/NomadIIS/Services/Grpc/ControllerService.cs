using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Plugin;
using System.Threading.Tasks;

namespace NomadIIS.Services.Grpc;

public sealed class ControllerService : GRPCController.GRPCControllerBase
{
	private readonly IHostApplicationLifetime _appLifetime;

	public ControllerService ( IHostApplicationLifetime appLifetime )
	{
		_appLifetime = appLifetime;
	}

	public override Task<Empty> Shutdown ( Empty request, ServerCallContext context )
	{
		_appLifetime.StopApplication();

		return Task.FromResult( new Empty() );
	}
}
