using System.Text;

namespace PublicServiceCaseManagement.Api.Configuration;

/// <summary>
/// Configuration for short-lived JWT access tokens issued to API clients.
/// The signing key is a symmetric secret and must be supplied by the host
/// (environment variable or secret store); it is never committed.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "CivicFlow";

    public string Audience { get; set; } = "CivicFlow.Api";

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Access-token lifetime in minutes. Tokens are deliberately short-lived;
    /// API clients can exchange their personal access token again for a new
    /// access token when the current one expires.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Issuer) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        Encoding.UTF8.GetByteCount(SigningKey ?? string.Empty) >=
            MinimumSigningKeyBytes &&
        AccessTokenLifetimeMinutes is >= 1 and <= 60;
}
