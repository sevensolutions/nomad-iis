using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NomadIIS;
using NomadIIS.Services;
using NomadIIS.Services.Grpc;
using Serilog;
using Serilog.Filters;
using System;
using System.Net;
using System.Security.Principal;

#pragma warning disable CA1416 // Plattformkompatibilit�t �berpr�fen
using ( var identity = WindowsIdentity.GetCurrent() )
{
	var principal = new WindowsPrincipal( identity );
	if ( !principal.IsInRole( WindowsBuiltInRole.Administrator ) )
	{
		Console.WriteLine( "Error: This plugin needs to be executed with administrator privileges." );
		return -1;
	}
}
#pragma warning restore CA1416 // Plattformkompatibilit�t �berpr�fen

var excludeRouting = Matching.FromSource( "Microsoft.AspNetCore.Routing" );
var excludeHosting = Matching.FromSource( "Microsoft.AspNetCore.Hosting" );

Log.Logger = new LoggerConfiguration()
	.MinimumLevel.Information()
	.Filter.ByExcluding( le => excludeRouting( le ) || excludeHosting( le ) )
	.WriteTo.File( "nomad_iis.log", outputTemplate: "{Timestamp:HH:mm:ss} [{ThreadId}] {Level:u3} {SourceContext}: {Message:lj}{NewLine}{Exception}" )
	.CreateLogger();

var builder = WebApplication.CreateBuilder( args );

builder.Logging.ClearProviders();
builder.Host.UseSerilog();

//System.Diagnostics.Debugger.Launch();

var grpcPort = builder.Configuration.GetValue( "port", 5003 );

#if MANAGEMENT_API
var managementApiPort = builder.Configuration.GetValue( "management-api-port", 0 );
#endif

builder.WebHost.ConfigureKestrel( config =>
{
	config.Listen( IPAddress.Loopback, grpcPort, listenOptions =>
	{
		listenOptions.Protocols = HttpProtocols.Http2;
	} );

#if MANAGEMENT_API
	if ( managementApiPort > 0 )
	{
		// Needed for the /upload API because ZipArchive.Extract() is synchronous.
		config.AllowSynchronousIO = true;

		config.Listen( IPAddress.Any, managementApiPort, listenOptions =>
		{
			listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
		} );
	}
#endif

	//config.ListenUnixSocket("/my-socket2.sock", listenOptions =>
	//{
	//    listenOptions.Protocols = HttpProtocols.Http2;
	//});
} );

builder.Services.AddSingleton<ManagementService>();

builder.Services.AddHostedService( sp => sp.GetRequiredService<ManagementService>() );
builder.Services.AddHostedService<HandshakeService>();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services
	.AddGrpcHealthChecks()
	.AddCheck( "plugin", () => HealthCheckResult.Healthy( "SERVING" ) );

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseRouting();

if ( app.Environment.IsDevelopment() )
	app.MapGrpcReflectionService();

// TODO: Limit them to the gRPC endpoint
app.MapGrpcService<ControllerService>();//.RequireHost( $"*:{grpcPort}" );
app.MapGrpcService<BrokerService>();//.RequireHost( $"*:{grpcPort}" );
app.MapGrpcService<StdioService>();//.RequireHost( $"*:{grpcPort}" );
app.MapGrpcService<BaseService>();//.RequireHost( $"*:{grpcPort}" );
app.MapGrpcService<DriverService>();//.RequireHost( $"*:{grpcPort}" );

#if MANAGEMENT_API
if ( managementApiPort > 0 )
{
	ManagementApi.Map( app )
		.RequireHost( $"*:{managementApiPort}" );
}
#endif

app.MapGet( "/", () => "This binary is a plugin. These are not meant to be executed directly. Please execute the program that consumes these plugins, which will load any plugins automatically." );

app.Run();

return 0;
