using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NomadIIS.Services;
using NomadIIS.Services.Grpc;
using Serilog;
using Serilog.Filters;
using System.Net;

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

//Debugger.Launch();

builder.WebHost.ConfigureKestrel( config => {
	config.Listen( IPAddress.Loopback, 5003, listenOptions => {
		listenOptions.Protocols = HttpProtocols.Http2;
	} );
	//config.ListenUnixSocket("/my-socket2.sock", listenOptions =>
	//{
	//    listenOptions.Protocols = HttpProtocols.Http2;
	//});
} );

builder.Services.AddSingleton<ManagementService>();

builder.Services.AddHostedService<HandshakeService>();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services
	.AddGrpcHealthChecks()
	.AddCheck( "plugin", () => HealthCheckResult.Healthy( "SERVING" ) );

var app = builder.Build();

app.UseRouting();

if ( app.Environment.IsDevelopment() )
	app.MapGrpcReflectionService();

app.MapGrpcService<ControllerService>();
app.MapGrpcService<BrokerService>();
app.MapGrpcService<StdioService>();
app.MapGrpcService<BaseService>();
app.MapGrpcService<DriverService>();

app.MapGet( "/", () => "This binary is a plugin. These are not meant to be executed directly. Please execute the program that consumes these plugins, which will load any plugins automatically." );

app.Run();
