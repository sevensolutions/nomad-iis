using Grpc.Core;
using Hashicorp.Nomad.Plugins.Base.Proto;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
using NomadIIS.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Hashicorp.Nomad.Plugins.Base.Proto.BasePlugin;

namespace NomadIIS.Services.Grpc;

public sealed class BaseService : BasePluginBase
{
	private readonly ILogger<BaseService> _logger;
	private readonly ManagementService _managementService;

	public BaseService ( ILogger<BaseService> logger, ManagementService managementService )
	{
		_logger = logger;
		_managementService = managementService;
	}

	public override Task<PluginInfoResponse> PluginInfo ( PluginInfoRequest request, ServerCallContext context )
	{
		_logger.LogInformation( nameof( PluginInfo ) );

		return Task.FromResult( new PluginInfoResponse()
		{
			Type = PluginType.Driver,
			Name = NomadIIS.PluginInfo.Name,
			PluginVersion = NomadIIS.PluginInfo.Version,
			PluginApiVersions =
			{
				{ "0.1.0" }
			}
		} );
	}

	public override Task<ConfigSchemaResponse> ConfigSchema ( ConfigSchemaRequest request, ServerCallContext context )
	{
		_logger.LogInformation( nameof( ConfigSchema ) );

		return Task.FromResult( new ConfigSchemaResponse()
		{
			Spec = HclSpecGenerator.Generate<DriverConfig>()
		} );
	}

	public override Task<SetConfigResponse> SetConfig ( SetConfigRequest request, ServerCallContext context )
	{
		_logger.LogInformation( nameof( SetConfig ) );

		if ( request.MsgpackConfig is not null )
		{
			var config = Configuration.MessagePackHelper.Deserialize<DriverConfig>( request.MsgpackConfig );

			_managementService.Configure( config );
		}

		return Task.FromResult( new SetConfigResponse() );
	}
}
