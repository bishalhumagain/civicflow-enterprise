using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Authenticates requests that present a personal access token as
/// <c>Authorization: Bearer civicflow_pat_&lt;id&gt;_&lt;secret&gt;</c>.
///
/// The token is looked up by its public id, its SHA-256 hash is compared in
/// constant time, and the request is rejected when the token is unknown,
/// revoked, expired, or the owning user is inactive. Roles are loaded from
/// ASP.NET Core Identity so the same authorization policies apply to token
/// and cookie callers.
/// </summary>
public sealed class PersonalAccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    public const string SchemeName = "PersonalAccessToken";

    private static readonly TimeSpan UsageTrackingInterval =
        TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // No bearer token: let other schemes (cookie) handle the request.
            return AuthenticateResult.NoResult();
        }

        var plaintext = header["Bearer ".Length..].Trim();

        if (!plaintext.StartsWith(
                PersonalAccessToken.TokenPrefix + "_",
                StringComparison.Ordinal))
        {
            // A non-PAT bearer value is not ours to validate.
            return AuthenticateResult.NoResult();
        }

        var publicId = PersonalAccessToken.ExtractPublicId(plaintext);

        if (publicId is null)
        {
            return AuthenticateResult.Fail("Malformed access token.");
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var token = await dbContext.PersonalAccessTokens
            .SingleOrDefaultAsync(
                candidate => candidate.PublicId == publicId);

        if (token is null)
        {
            return AuthenticateResult.Fail("Unknown access token.");
        }

        var presentedHash = PersonalAccessToken.Hash(plaintext);

        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(presentedHash),
                System.Text.Encoding.UTF8.GetBytes(token.TokenHash)))
        {
            return AuthenticateResult.Fail("Invalid access token.");
        }

        var now = timeProvider.GetUtcNow();

        if (!token.IsActive(now))
        {
            return AuthenticateResult.Fail("Expired or revoked access token.");
        }

        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == token.UserId);

        if (user is null || !user.IsActive)
        {
            return AuthenticateResult.Fail("Access token owner is inactive.");
        }

        var roles = await (
            from userRole in dbContext.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
            join role in dbContext.Set<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
                on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
            select role.Name)
            .Where(name => name != null)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role!));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        await RecordUsageAsync(dbContext, token, now);

        return AuthenticateResult.Success(ticket);
    }

    /// <summary>
    /// Records when a token was last used, at most once per
    /// <see cref="UsageTrackingInterval"/> to avoid a database write on every
    /// single request.
    /// </summary>
    private static async Task RecordUsageAsync(
        CivicFlowDbContext dbContext,
        PersonalAccessToken token,
        DateTimeOffset now)
    {
        if (token.LastUsedAt is { } lastUsed &&
            now - lastUsed < UsageTrackingInterval)
        {
            return;
        }

        token.MarkUsed(now);

        await dbContext.SaveChangesAsync();
    }
}
