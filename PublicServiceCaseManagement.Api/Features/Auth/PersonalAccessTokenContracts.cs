namespace PublicServiceCaseManagement.Api.Features.Auth;

public static class PersonalAccessTokenRoutes
{
    public const string Base = "api/auth/tokens";
}

/// <summary>
/// Request to mint a new personal access token. <see cref="ExpiresInDays"/>
/// is optional; when omitted the token does not expire.
/// </summary>
public sealed record CreatePersonalAccessTokenRequest(
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string Name,
    [System.ComponentModel.DataAnnotations.Range(1, 3650)]
    int? ExpiresInDays = null);

/// <summary>
/// Metadata for an existing token. The plaintext value is never returned
/// here; it is only present on the create response.
/// </summary>
public sealed record PersonalAccessTokenResponse(
    Guid Id,
    string Name,
    string PublicId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    bool IsActive);

/// <summary>
/// Create response. <see cref="Token"/> is the one-time plaintext value and
/// is shown to the caller only once.
/// </summary>
public sealed record CreatePersonalAccessTokenResponse(
    Guid Id,
    string Name,
    string Token,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);
