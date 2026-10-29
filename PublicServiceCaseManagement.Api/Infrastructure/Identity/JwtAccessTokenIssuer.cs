using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PublicServiceCaseManagement.Api.Configuration;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

public sealed record IssuedAccessToken(
    string Token,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);

public interface IJwtAccessTokenIssuer
{
    IssuedAccessToken Issue(
        Guid userId,
        string fullName,
        string email,
        IEnumerable<string> roles);
}

/// <summary>
/// Issues short-lived, signed JWT access tokens. The token contains only
/// identity and role claims plus standard JWT metadata; it never contains a
/// password, personal access token, security stamp, or other credential.
/// Organization context is intentionally not embedded: it is resolved from
/// the database for the selected <c>X-Organization-Id</c> on every request,
/// exactly like cookie and personal-access-token requests.
/// </summary>
public sealed class JwtAccessTokenIssuer(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IJwtAccessTokenIssuer
{
    public IssuedAccessToken Issue(
        Guid userId,
        string fullName,
        string email,
        IEnumerable<string> roles)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "A user id is required.",
                nameof(userId));
        }

        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, fullName ?? string.Empty),
            new(ClaimTypes.Email, email ?? string.Empty)
        };

        claims.AddRange(
            roles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.Ordinal)
                .Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(settings.SigningKey));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new IssuedAccessToken(
            encoded,
            expiresAt,
            (int)Math.Max(0, (expiresAt - now).TotalSeconds));
    }
}
