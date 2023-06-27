using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NomadIIS.Services;

public sealed class HandshakeService : IHostedService
{
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly IServer _server;

	public HandshakeService ( IHostApplicationLifetime appLifetime, IServer server )
	{
		_appLifetime = appLifetime;
		_server = server;
	}

	public Task StartAsync ( CancellationToken cancellationToken )
	{
		_appLifetime.ApplicationStarted.Register( OnServerStarted );

		return Task.CompletedTask;
	}
	public Task StopAsync ( CancellationToken cancellationToken ) => Task.CompletedTask;

	private void OnServerStarted ()
	{
		var addressFeature = _server.Features.GetRequiredFeature<IServerAddressesFeature>();
		var address = addressFeature.Addresses.First();

		string connection = "";
		if ( address.StartsWith( "http://" ) )
			connection = $"tcp|{address.Replace( "http://", "" )}";
		else if ( address.StartsWith( "unix:" ) )
			connection = $"unix|{connection.Replace( "unix:", "" )}";

		Console.Write( $"1|2|{connection}|grpc\n" );
	}
}
