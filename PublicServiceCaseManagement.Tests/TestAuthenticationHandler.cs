using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Test authentication scheme that authenticates requests without a real
/// cookie. The authenticated user id is supplied through the
/// <c>X-Test-User-Id</c> header (or a well-known default), and the role
/// through <c>X-Test-Role</c>. This mirrors the claims that ASP.NET Core
/// Identity places in the cookie principal.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string SchemeName = "CivicFlowTest";

    public const string StaffRole = AppRoles.Staff;
    public const string CitizenRole = AppRoles.Citizen;
    public const string SuperAdminRole = AppRoles.SuperAdmin;

    public static readonly Guid DefaultUserId =
        Guid.Parse("a0000000-0000-0000-0000-000000000001");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // A caller presenting a bearer token must not be authenticated by
        // this scheme; the request is treated as unauthenticated (401).
        if (Request.Headers.Authorization.ToString()
            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers["X-Test-User-Id"].FirstOrDefault()
            is { Length: > 0 } raw && Guid.TryParse(raw, out var parsed)
                ? parsed
                : DefaultUserId;

        var role = Request.Headers["X-Test-Role"].FirstOrDefault()
                   ?? StaffRole;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "CivicFlow Integration Test User"),
            new(ClaimTypes.Email, "integration.test@civicflow.test"),
            new(ClaimTypes.Role, role)
        };

        if (string.Equals(role, StaffRole, StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("organization.manage", "true"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
