namespace PublicServiceCaseManagement.Api.Domain.Staff;

public sealed class StaffMember
{
    private StaffMember()
    {
    }

    private StaffMember(
        Guid organizationId,
        string fullName,
        string email,
        string? phoneNumber,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        FullName = fullName;
        Email = email;
        PhoneNumber = phoneNumber;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// The contact phone number collected when the staff member was
    /// onboarded. It is optional.
    /// </summary>
    public string? PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static StaffMember Create(
        Guid organizationId,
        string fullName,
        string email,
        string? phoneNumber,
        DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Full name is required and must not exceed 150 characters.",
                nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(email) ||
            email.Trim().Length > 254 ||
            !System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var address) ||
            !string.Equals(
                address.Address,
                email.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A valid email address is required.",
                nameof(email));
        }

        return new StaffMember(
            organizationId,
            fullName.Trim(),
            email.Trim(),
            PhoneNumbers.Normalize(phoneNumber),
            now);
    }

    public void UpdatePhoneNumber(string? phoneNumber, DateTimeOffset now)
    {
        PhoneNumber = PhoneNumbers.Normalize(phoneNumber);
        UpdatedAt = now;
    }

    /// <summary>
    /// Applies the self-service profile details a staff member is allowed to
    /// change on their own account. The organization, role and activation
    /// state stay under administrator control.
    /// </summary>
    public void UpdateProfile(
        string fullName,
        string? phoneNumber,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Full name is required and must not exceed 150 characters.",
                nameof(fullName));
        }

        FullName = fullName.Trim();
        PhoneNumber = PhoneNumbers.Normalize(phoneNumber);
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
