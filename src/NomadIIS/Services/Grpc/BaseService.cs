﻿using Grpc.Core;
using Hashicorp.Nomad.Plugins.Base.Proto;
using MessagePack;
using Microsoft.Extensions.Logging;
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
	private static readonly Regex _intervalRegex = new Regex( @"^(?<value>\d+)(?<unit>s)$" );

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
			Name = "iis",
			PluginVersion = "1.0",
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
			Spec = ConfigSchemas.DriverConfig
		} );
	}

	public override Task<SetConfigResponse> SetConfig ( SetConfigRequest request, ServerCallContext context )
	{
		_logger.LogInformation( nameof( SetConfig ) );

		var statsInterval = TimeSpan.FromSeconds( 3 );

		if ( request.MsgpackConfig is not null )
		{
			var config = MessagePackSerializer.Deserialize<Dictionary<object, object>>( request.MsgpackConfig.Memory );

			if ( config.TryGetValue( "stats_interval", out var objStatsInterval )
				&& objStatsInterval is string strStatsInterval &&
				!string.IsNullOrEmpty( strStatsInterval ) )
			{
				var match = _intervalRegex.Match( strStatsInterval );

				if ( match.Success )
				{
					var value = int.Parse( match.Groups["value"].Value );

					if ( value < 1 )
						throw new ArgumentException( $"stats_interval must be at least 1s." );

					statsInterval = TimeSpan.FromSeconds( value );
				}
				else
					throw new ArgumentException( $"Invalid value for stats_interval configuration value" );
			}
		}

		_managementService.Configure( statsInterval );

		return Task.FromResult( new SetConfigResponse() );
	}
}