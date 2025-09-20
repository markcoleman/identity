using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IdentityResolution.Api;

/// <summary>
/// Development authentication handler that allows all requests to satisfy authorization requirements.
/// In production, this would be replaced with proper JWT or other authentication mechanisms.
/// </summary>
public class DevelopmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // For development/demo purposes, create a mock authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "DevelopmentUser"),
            new Claim(ClaimTypes.NameIdentifier, "dev-user-id")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
