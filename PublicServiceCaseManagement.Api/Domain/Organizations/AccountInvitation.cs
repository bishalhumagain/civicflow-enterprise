using PublicServiceCaseManagement.Api.Domain.Common;

namespace PublicServiceCaseManagement.Api.Domain.Organizations;

/// <summary>
/// A single-use invitation that lets an invited person choose their own
/// password. Only a hash of the invitation token is stored, so a database
/// disclosure cannot be replayed against the public acceptance endpoint.
/// </summary>
public sealed class AccountInvitation
{
    private AccountInvitation()
    {
    }

    private AccountInvitation(
        Guid organizationId,
        string email,
        string fullName,
        string? phoneNumber,
        OrganizationRole organizationRole,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Email = email;
        NormalizedEmail = email.ToUpperInvariant();
        FullName = fullName;
        PhoneNumber = phoneNumber;
        OrganizationRole = organizationRole;
        TokenHash = tokenHash;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// The contact phone number the administrator collected for the invited
    /// person. It is optional and stored as plain text.
    /// </summary>
    public string? PhoneNumber { get; private set; }

    public OrganizationRole OrganizationRole { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? AcceptedUserId { get; private set; }

    /// <summary>
    /// Set when the most recent delivery attempt failed. The invitation
    /// itself remains valid so an administrator can retry delivery.
    /// </summary>
    public DateTimeOffset? DeliveryFailedAt { get; private set; }

    /// <summary>
    /// A short, non-sensitive description of the last delivery failure.
    /// Never contains the invitation token or credentials.
    /// </summary>
    public string? DeliveryError { get; private set; }

    public bool IsDeliveryFailed => DeliveryFailedAt.HasValue;

    public bool IsUsed => UsedAt.HasValue;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public bool IsAvailable(DateTimeOffset now) =>
        !IsUsed && !IsRevoked && !IsExpired(now);

    public static AccountInvitation Create(
        Guid organizationId,
        string email,
        string fullName,
        string? phoneNumber,
        OrganizationRole organizationRole,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(email) ||
            email.Trim().Length > 254 ||
            !System.Net.Mail.MailAddress.TryCreate(
                email.Trim(),
                out var address) ||
            !string.Equals(
                address.Address,
                email.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A valid email address is required.",
                nameof(email));
        }

        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Full name is required and must not exceed 150 characters.",
                nameof(fullName));
        }

        if (!Enum.IsDefined(organizationRole))
        {
            throw new ArgumentOutOfRangeException(
                nameof(organizationRole),
                "The organization role is invalid.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "A token hash is required.",
                nameof(tokenHash));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "The inviting user is required.",
                nameof(createdByUserId));
        }

        if (expiresAt <= now)
        {
            throw new ArgumentException(
                "The invitation expiry must be in the future.",
                nameof(expiresAt));
        }

        return new AccountInvitation(
            organizationId,
            TextNormalization.NormalizeEmail(email, nameof(email)),
            TextNormalization.NormalizeName(fullName),
            PhoneNumbers.Normalize(phoneNumber),
            organizationRole,
            tokenHash,
            createdByUserId,
            now,
            expiresAt);
    }

    public void MarkDeliverySucceeded(DateTimeOffset now)
    {
        if (!IsDeliveryFailed)
        {
            return;
        }

        DeliveryFailedAt = null;
        DeliveryError = null;
        UpdatedAt = now;
    }

    public void MarkDeliveryFailed(string? error, DateTimeOffset now)
    {
        DeliveryFailedAt = now;
        DeliveryError = NormalizeDeliveryError(error);
        UpdatedAt = now;
    }

    public void MarkUsed(Guid acceptedUserId, DateTimeOffset now)
    {
        if (acceptedUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "The accepted user is required.",
                nameof(acceptedUserId));
        }

        if (!IsAvailable(now))
        {
            throw new InvalidOperationException(
                "The invitation is no longer available.");
        }

        UsedAt = now;
        AcceptedUserId = acceptedUserId;
        UpdatedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (IsUsed)
        {
            throw new InvalidOperationException(
                "A used invitation cannot be revoked.");
        }

        if (IsRevoked)
        {
            return;
        }

        RevokedAt = now;
        UpdatedAt = now;
    }

    public void Reissue(
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "A token hash is required.",
                nameof(tokenHash));
        }

        if (IsUsed || IsRevoked)
        {
            throw new InvalidOperationException(
                "Only a pending invitation can be reissued.");
        }

        if (expiresAt <= now)
        {
            throw new ArgumentException(
                "The invitation expiry must be in the future.",
                nameof(expiresAt));
        }

        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        UpdatedAt = now;
    }

    private static string? NormalizeDeliveryError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var trimmed = error.Trim();

        return trimmed.Length <= 500
            ? trimmed
            : trimmed[..500];
    }
}
