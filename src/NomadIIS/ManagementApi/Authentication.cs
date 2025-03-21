#if MANAGEMENT_API
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NomadIIS.ManagementApi;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
		public string? ApiJwtSecret { get; set; }
	}

	public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
	{
		public ApiKeyAuthenticationHandler ( IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder )
			: base( options, logger, encoder )
		{
		}

		protected override async Task<AuthenticateResult> HandleAuthenticateAsync ()
		{
			if ( string.IsNullOrEmpty( Options.ApiKey ) && string.IsNullOrEmpty( Options.ApiJwtSecret ) )
			{
				return AuthenticateResult.Success(
					new AuthenticationTicket( new ClaimsPrincipal( new ClaimsIdentity( Scheme.Name ) ), Scheme.Name ) );
			}

			if ( !Request.Headers.ContainsKey( Options.HeaderName ) )
				return AuthenticateResult.Fail( $"Missing header {Options.HeaderName}." );

			string headerValue = Request.Headers[Options.HeaderName]!;

			if ( !string.IsNullOrEmpty( Options.ApiKey ) && headerValue == Options.ApiKey )
			{
				var claimsIdentity = new ClaimsIdentity( Scheme.Name );

				// Api-Key auth doesn't support custom claims, so we permit everything.
				claimsIdentity.AddClaim( new Claim( "namespace", "*" ) );
				claimsIdentity.AddClaim( new Claim( "job", "*" ) );
				claimsIdentity.AddClaim( new Claim( "allocId", "*" ) );

				var principal = new ClaimsPrincipal( claimsIdentity );

				var ticket = new AuthenticationTicket( principal, Scheme.Name );

				return AuthenticateResult.Success( ticket );
			}

			if ( !string.IsNullOrEmpty( Options.ApiJwtSecret ) )
			{
				var validationResult = await new JwtSecurityTokenHandler()
					.ValidateTokenAsync( headerValue, new TokenValidationParameters()
					{
						ValidateLifetime = true,
						ValidateIssuerSigningKey = true,
						IssuerSigningKey = new SymmetricSecurityKey( Encoding.UTF8.GetBytes( Options.ApiJwtSecret ) )
						{
							KeyId = "static"
						},
						ValidIssuer = "NomadIIS",
						ValidateIssuer = true,
						ValidAudience = "ManagementApi",
						ValidateAudience = true
					} );

				if ( !validationResult.IsValid )
				{
					Logger.LogWarning( validationResult.Exception, "Received invalid JWT token for Management API." );

					return AuthenticateResult.Fail( "Invalid token." );
				}

				var principal = new ClaimsPrincipal( validationResult.ClaimsIdentity );

				var ticket = new AuthenticationTicket( principal, Scheme.Name );

				return AuthenticateResult.Success( ticket );
			}

			return AuthenticateResult.Fail( "Invalid token." );
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
