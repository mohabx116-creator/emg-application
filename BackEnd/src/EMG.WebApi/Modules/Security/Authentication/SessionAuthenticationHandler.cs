using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using EMG.Api.Modules.Security.Services;

namespace EMG.Api.Modules.Security.Authentication;

public sealed class SessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IAuthSessionService sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder, clock)
{
    public new const string Scheme = "EmgSession";
    public const string CookieName = "emg_session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var secret) || string.IsNullOrWhiteSpace(secret)) return AuthenticateResult.NoResult();
        var user = await sessions.ValidateAsync(secret, Context.RequestAborted);
        if (user is null) return AuthenticateResult.Fail("Session is invalid or expired.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(user.UserRoles.Select(link => new Claim(ClaimTypes.Role, link.Role.Name)));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)), Scheme));
    }
}
