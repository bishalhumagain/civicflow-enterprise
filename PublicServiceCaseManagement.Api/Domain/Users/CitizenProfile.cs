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
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 150)
            throw new ArgumentException("Full name is required and must not exceed 150 characters.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 254)
            throw new ArgumentException("A valid email address is required.", nameof(email));
        if (age.HasValue && (age.Value < 0 || age.Value > 150))
            throw new ArgumentException("Age must be between 0 and 150.", nameof(age));

        return new CitizenProfile(
            applicationUserId,
            fullName.Trim(),
            email.Trim(),
            phoneNumber?.Trim(),
            nidNumber?.Trim(),
            citizenshipNumber?.Trim(),
            age,
            gender?.Trim(),
            permanentAddress?.Trim(),
            temporaryAddress?.Trim(),
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
            if (fullName.Trim().Length > 150)
                throw new ArgumentException("Full name must not exceed 150 characters.", nameof(fullName));

            FullName = fullName.Trim();
        }

        PhoneNumber = phoneNumber?.Trim();
        NIDNumber = nidNumber?.Trim();
        CitizenshipNumber = citizenshipNumber?.Trim();
        Age = age;
        Gender = gender?.Trim();
        PermanentAddress = permanentAddress?.Trim();
        TemporaryAddress = temporaryAddress?.Trim();
        UpdatedAt = now;
    }
}
