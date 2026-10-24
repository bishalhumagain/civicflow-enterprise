namespace PublicServiceCaseManagement.Api.Domain.Organizations;

public sealed class OrganizationType
{
    private OrganizationType()
    {
    }

    private OrganizationType(
        string name,
        string code,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name;
        Code = code;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrganizationType Create(
        string name,
        string code,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Organization type name is required and must not exceed 200 characters.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(code) ||
            code.Trim().Length > 50)
        {
            throw new ArgumentException(
                "Organization type code is required and must not exceed 50 characters.",
                nameof(code));
        }

        return new OrganizationType(
            name.Trim(),
            code.Trim().ToUpperInvariant(),
            now);
    }

    public void UpdateDetails(
        string name,
        string code,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Organization type name is required and must not exceed 200 characters.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(code) ||
            code.Trim().Length > 50)
        {
            throw new ArgumentException(
                "Organization type code is required and must not exceed 50 characters.",
                nameof(code));
        }

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
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
