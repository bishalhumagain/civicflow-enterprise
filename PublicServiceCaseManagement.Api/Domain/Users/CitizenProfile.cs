using PublicServiceCaseManagement.Api.Domain;

using PublicServiceCaseManagement.Api.Domain.Common;

namespace PublicServiceCaseManagement.Api.Domain.Users;

public sealed class CitizenProfile
{
    private CitizenProfile()
    {
    }

    private CitizenProfile(
        Guid applicationUserId,
        string fullName,
        string email,
        string? phoneNumber,
        string? nidNumber,
        string? citizenshipNumber,
        int? age,
        string? gender,
        string? permanentAddress,
        string? temporaryAddress,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        ApplicationUserId = applicationUserId;
        FullName = fullName;
        Email = email;
        PhoneNumber = phoneNumber;
        NIDNumber = nidNumber;
        CitizenshipNumber = citizenshipNumber;
        Age = age;
        Gender = gender;
        PermanentAddress = permanentAddress;
        TemporaryAddress = temporaryAddress;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ApplicationUserId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? NIDNumber { get; private set; }
    public string? CitizenshipNumber { get; private set; }
    public int? Age { get; private set; }
    public string? Gender { get; private set; }
    public string? PermanentAddress { get; private set; }
    public string? TemporaryAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static CitizenProfile Create(
        Guid applicationUserId,
        string fullName,
        string email,
        string? phoneNumber,
        string? nidNumber,
        string? citizenshipNumber,
        int? age,
        string? gender,
        string? permanentAddress,
        string? temporaryAddress,
        DateTimeOffset now)
    {
        if (applicationUserId == Guid.Empty)
            throw new ArgumentException("Application user ID is required.", nameof(applicationUserId));
        var normalizedName = TextNormalization.NormalizeName(fullName);
        var normalizedEmail = TextNormalization.NormalizeEmail(email, nameof(email));
        if (normalizedEmail.Length > 254)
            throw new ArgumentException("A valid email address is required.", nameof(email));
        if (age.HasValue && (age.Value < 0 || age.Value > 150))
            throw new ArgumentException("Age must be between 0 and 150.", nameof(age));

        return new CitizenProfile(
            applicationUserId,
            normalizedName,
            normalizedEmail,
            PhoneNumbers.Normalize(phoneNumber),
            TextNormalization.NormalizeOptionalIdentifier(nidNumber),
            TextNormalization.NormalizeOptionalIdentifier(citizenshipNumber),
            age,
            TextNormalization.NormalizeOptional(gender),
            TextNormalization.NormalizeOptional(permanentAddress),
            TextNormalization.NormalizeOptional(temporaryAddress),
            now);
    }

    public void Update(
        string? fullName,
        string? phoneNumber,
        string? nidNumber,
        string? citizenshipNumber,
        int? age,
        string? gender,
        string? permanentAddress,
        string? temporaryAddress,
        DateTimeOffset now)
    {
        if (age.HasValue && (age.Value < 0 || age.Value > 150))
            throw new ArgumentException("Age must be between 0 and 150.", nameof(age));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            FullName = TextNormalization.NormalizeName(fullName);
        }

        PhoneNumber = PhoneNumbers.Normalize(phoneNumber);
        NIDNumber = TextNormalization.NormalizeOptionalIdentifier(nidNumber);
        CitizenshipNumber = TextNormalization.NormalizeOptionalIdentifier(
            citizenshipNumber);
        Age = age;
        Gender = TextNormalization.NormalizeOptional(gender);
        PermanentAddress = TextNormalization.NormalizeOptional(permanentAddress);
        TemporaryAddress = TextNormalization.NormalizeOptional(temporaryAddress);
        UpdatedAt = now;
    }
}
