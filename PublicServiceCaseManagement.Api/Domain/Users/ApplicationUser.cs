
namespace PublicServiceCaseManagement.Api.Domain.Users;

public sealed class ApplicationUser
{
    private ApplicationUser()
    {
    }

    private ApplicationUser(
        string fullName,
        string email,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        Email = email;
        NormalizedEmail = email.ToUpperInvariant();
        PlatformRole = PlatformRole.None;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string? ExternalIdentityIssuer { get; private set; }

    public string? ExternalIdentitySubject { get; private set; }

    public PlatformRole PlatformRole { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

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

        return new ApplicationUser(
            fullName.Trim(),
            email.Trim(),
            now);
    }

    public void LinkExternalIdentity(
        string issuer,
        string subject,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(issuer) ||
            issuer.Trim().Length > 500 ||
            !Uri.TryCreate(issuer.Trim(), UriKind.Absolute, out _))
        {
            throw new ArgumentException(
                "A valid absolute identity issuer URL is required.",
                nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(subject) ||
            subject.Trim().Length > 255)
        {
            throw new ArgumentException(
                "An identity subject is required and must not exceed 255 characters.",
                nameof(subject));
        }

        var normalizedIssuer = issuer.Trim();
        var normalizedSubject = subject.Trim();

        if (ExternalIdentityIssuer is not null ||
            ExternalIdentitySubject is not null)
        {
            if (string.Equals(
                    ExternalIdentityIssuer,
                    normalizedIssuer,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ExternalIdentitySubject,
                    normalizedSubject,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "This application user is already linked to an external identity.");
        }

        ExternalIdentityIssuer = normalizedIssuer;
        ExternalIdentitySubject = normalizedSubject;
        UpdatedAt = now;
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
}
