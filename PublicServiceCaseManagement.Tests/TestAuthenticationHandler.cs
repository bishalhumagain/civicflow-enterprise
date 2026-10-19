using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PublicServiceCaseManagement.Tests;

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string SchemeName = "CivicFlowTest";
    public const string Issuer = "http://localhost:8080/realms/civicflow";
    public const string Subject = "civicflow-integration-test-user";
    public const string StaffClientId = "civicflow-staff";
    public const string CitizenClientId = "civicflow-citizen";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (Request.Headers.Authorization.ToString()
            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Test bearer token is invalid."));
        }

        var clientId = Request.Headers["X-Test-Client-Id"].FirstOrDefault()
                       ?? StaffClientId;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Subject),
            new Claim(ClaimTypes.Name, "CivicFlow Integration Test User"),
            new Claim("iss", Issuer),
            new Claim("sub", Subject),
            new Claim("azp", clientId)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}