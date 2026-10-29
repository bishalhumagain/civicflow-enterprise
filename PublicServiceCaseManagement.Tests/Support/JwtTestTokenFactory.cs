using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PublicServiceCaseManagement.Tests.Support;

/// <summary>
/// Builds JWT strings with explicit issuer, audience, key, and lifetime so
/// validation tests can exercise each rejection path independently of the
/// production issuer.
/// </summary>
public static class JwtTestTokenFactory
{
    public static string Create(
        Guid userId,
        IEnumerable<string> roles,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        string issuer = CivicFlowApiFactory.TestJwtIssuer,
        string audience = CivicFlowApiFactory.TestJwtAudience,
        string signingKey = CivicFlowApiFactory.TestJwtSigningKey)
    {
        var now = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "JWT Integration Test User"),
            new(ClaimTypes.Email, "jwt.integration@example.test")
        };

        claims.AddRange(
            roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: (notBefore ?? now.AddMinutes(-1)).UtcDateTime,
            expires: (expires ?? now.AddMinutes(15)).UtcDateTime,
            signingCredentials: new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
