using System.Security.Cryptography;

namespace PublicServiceCaseManagement.Api.Domain.Users;

/// <summary>
/// A server-issued personal access token that lets a non-browser client
/// (mobile app, CLI, integration) call the API with
/// <c>Authorization: Bearer &lt;token&gt;</c>.
///
/// Only a SHA-256 hash of the token is persisted; the plaintext token is
/// shown to the caller exactly once at creation time. Tokens are scoped to a
/// single user, can expire, and can be revoked individually.
/// </summary>
public sealed class PersonalAccessToken
{
    public const string TokenPrefix = "civicflow_pat";

    private PersonalAccessToken()
    {
    }

    private PersonalAccessToken(
        Guid userId,
        string name,
        string publicId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset? expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Name = name;
        PublicId = publicId;
        TokenHash = tokenHash;
        CreatedAt = now;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Public, non-secret identifier embedded in the token. Indexed so the
    /// token can be located before its hash is compared.
    /// </summary>
    public string PublicId { get; private set; } = string.Empty;

    /// <summary>
    /// Base64 SHA-256 hash of the full plaintext token. The plaintext is
    /// never stored.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) =>
        ExpiresAt is { } expiry && expiry <= now;

    public bool IsActive(DateTimeOffset now) =>
        !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Creates a new token, returning the entity and the one-time plaintext
    /// value that must be shown to the caller and never persisted.
    /// </summary>
    public static (PersonalAccessToken Token, string Plaintext) Create(
        Guid userId,
        string name,
        DateTimeOffset now,
        DateTimeOffset? expiresAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "A user id is required.",
                nameof(userId));
        }

        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0 || trimmedName.Length > 100)
        {
            throw new ArgumentException(
                "A token name is required and must not exceed 100 characters.",
                nameof(name));
        }

        if (expiresAt is { } expiry && expiry <= now)
        {
            throw new ArgumentException(
                "The expiry must be in the future.",
                nameof(expiresAt));
        }

        var publicId = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();

        // Hex keeps the secret URL-safe and free of the "_" separator used
        // by the token format, so the value can be embedded in a header or a
        // query string without escaping.
        var secret = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();

        var plaintext = $"{TokenPrefix}_{publicId}_{secret}";

        var token = new PersonalAccessToken(
            userId,
            trimmedName,
            publicId,
            Hash(plaintext),
            now,
            expiresAt);

        return (token, plaintext);
    }

    public void MarkUsed(DateTimeOffset now) => LastUsedAt = now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>
    /// Extracts the public identifier from a presented token, or null when
    /// the token does not have the expected shape.
    /// </summary>
    public static string? ExtractPublicId(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var parts = plaintext.Split('_', 4);

        // civicflow_pat_<publicId>_<secret>
        if (parts.Length != 4 ||
            !string.Equals(parts[0], "civicflow", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "pat", StringComparison.Ordinal) ||
            parts[2].Length == 0 ||
            parts[3].Length == 0)
        {
            return null;
        }

        return parts[2];
    }

    public static string Hash(string plaintext) =>
        Convert.ToBase64String(
            SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(plaintext)));
}
