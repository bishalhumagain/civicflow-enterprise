using System.Text.Json.Serialization;

namespace PublicServiceCaseManagement.Api.Features.Auth.Contracts;

public static class AccessTokenRoutes
{
    public const string Base = "api/auth/token";
}

/// <summary>
/// RFC 6749-style token response. <see cref="AccessToken"/> is a short-lived
/// signed JWT; the long-lived personal access token used to obtain it is
/// never echoed back.
/// </summary>
public sealed record AccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
