using Microsoft.AspNetCore.Identity;

using PublicServiceCaseManagement.Api.Domain.Common;

namespace PublicServiceCaseManagement.Api.Domain.Users;

/// <summary>
/// Application user backed by ASP.NET Core Identity. The primary key is a
/// <see cref="Guid"/> so existing organization memberships and citizen profiles
/// keep referencing the same user identifier that was previously stored.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public PlatformRole PlatformRole { get; set; } = PlatformRole.None;

    /// <summary>
    /// Administrative activation state. Distinct from Identity lockout, which
    /// handles temporary brute-force lockout. A deactivated account cannot
    /// authenticate even with valid credentials.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Creates a new user aggregate with the supplied profile details. The
    /// credential (password hash) is set separately through Identity APIs.
    /// </summary>
    public static ApplicationUser Create(
        string fullName,
        string email,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Full name is required and must not exceed 150 characters.",
                nameof(fullName));
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

        var normalizedEmail = email.Trim();

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = TextNormalization.NormalizeName(fullName),
            Email = normalizedEmail,
            UserName = normalizedEmail,
            NormalizedEmail = normalizedEmail.ToUpperInvariant(),
            NormalizedUserName = normalizedEmail.ToUpperInvariant(),
            PlatformRole = PlatformRole.None,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void SetPlatformRole(
        PlatformRole role,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                "The platform role is invalid.");
        }

        if (PlatformRole == role)
        {
            return;
        }

        PlatformRole = role;
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        UpdatedAt = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        UpdatedAt = now;
    }
}
