using Grpc.Core;
using Hashicorp.Nomad.Plugins.Base.Proto;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Web.Administration;
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
			Spec = ConfigSchemas.DriverConfig
		} );
	}

	public override Task<SetConfigResponse> SetConfig ( SetConfigRequest request, ServerCallContext context )
	{
		_logger.LogInformation( nameof( SetConfig ) );

		var enabled = true;
		var directorySecurity = true;
		TimeSpan? statsInterval = null;
		TimeSpan? fingerprintInterval = null;

		if ( request.MsgpackConfig is not null )
		{
			var config = MessagePackSerializer.Deserialize<Dictionary<object, object>>( request.MsgpackConfig.Memory );
			
			if ( config.TryGetValue( "enabled", out var rawEnabled ) && rawEnabled is bool vEnabled )
				enabled = vEnabled;

			if ( config.TryGetValue( "fingerprint_interval", out var objFingerprintInterval )
				&& objFingerprintInterval is string strFingerprintInterval &&
				!string.IsNullOrEmpty( strFingerprintInterval ) )
			{
				var interval = TimeSpanHelper.TryParse( strFingerprintInterval );

				if ( interval is null )
					throw new ArgumentException( $"Invalid value for fingerprint_interval configuration value" );
				if ( interval.Value < TimeSpan.FromSeconds( 10 ) )
					throw new ArgumentException( $"fingerprint_interval must be at least 10s." );

				fingerprintInterval = interval.Value;
			}

			if ( config.TryGetValue( "directory_security", out var rawDirectorySecurity ) && rawEnabled is bool vDirectorySecurity )
				directorySecurity = vDirectorySecurity;
		}

		_managementService.Configure( enabled, fingerprintInterval, directorySecurity );

		return Task.FromResult( new SetConfigResponse() );
	}
}
