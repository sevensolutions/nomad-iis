#if MANAGEMENT_API
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NomadIIS.ManagementApi;
using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace NomadIIS.ManagementApi
{
	public static class ApiKeyAuthenticationDefaults
	{
		public const string AuthenticationScheme = "ApiKey";
	}

	public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
	{
		public string HeaderName { get; set; } = "X-Api-Key";
		public string? ApiKey { get; set; }
	}

	public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
	{
		public ApiKeyAuthenticationHandler ( IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder )
			: base( options, logger, encoder )
		{
		}

		protected override Task<AuthenticateResult> HandleAuthenticateAsync ()
		{
			if ( string.IsNullOrEmpty( Options.ApiKey ) )
			{
				return Task.FromResult(
					AuthenticateResult.Success(
						new AuthenticationTicket( new ClaimsPrincipal( new ClaimsIdentity( Scheme.Name ) ), Scheme.Name ) ) );
			}

			if ( !Request.Headers.ContainsKey( Options.HeaderName ) )
				return Task.FromResult( AuthenticateResult.Fail( $"Missing header {Options.HeaderName}." ) );

			string headerValue = Request.Headers[Options.HeaderName]!;

			if ( headerValue != Options.ApiKey )
				return Task.FromResult( AuthenticateResult.Fail( "Invalid token." ) );

			var principal = new ClaimsPrincipal( new ClaimsIdentity( Scheme.Name ) );

			var ticket = new AuthenticationTicket( principal, Scheme.Name );

			return Task.FromResult( AuthenticateResult.Success( ticket ) );
		}
	}
}

namespace Microsoft.Extensions.DependencyInjection
{
	public static class ApiKeyExtensions
	{
		public static AuthenticationBuilder AddApiKey ( this AuthenticationBuilder builder )
			=> builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>( ApiKeyAuthenticationDefaults.AuthenticationScheme, _ => { } );
		public static AuthenticationBuilder AddApiKey ( this AuthenticationBuilder builder, Action<ApiKeyAuthenticationOptions>? options )
			=> builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>( ApiKeyAuthenticationDefaults.AuthenticationScheme, options );
	}
}

#endif
