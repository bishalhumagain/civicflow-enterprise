using PublicServiceCaseManagement.Api.Domain.Common;

namespace PublicServiceCaseManagement.Api.Domain.Organizations;

public sealed class Organization
{
    private Organization()
    {
    }

    private Organization(
        string name,
        string code,
        Guid organizationTypeId,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name;
        Code = code;
        OrganizationTypeId = organizationTypeId;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public Guid OrganizationTypeId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Organization Create(
        string name,
        string code,
        DateTimeOffset now,
        Guid organizationTypeId = default)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            TextNormalization.CollapseWhitespace(name).Length > 200)
        {
            throw new ArgumentException(
                "Organization name is required and must not exceed 200 characters.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(code) ||
            code.Trim().Length > 50)
        {
            throw new ArgumentException(
                "Organization code is required and must not exceed 50 characters.",
                nameof(code));
        }

        return new Organization(
            TextNormalization.CollapseWhitespace(name),
            code.Trim().ToUpperInvariant(),
            ResolveTypeId(organizationTypeId),
            now);
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

    public void UpdateDetails(
        string name,
        string code,
        DateTimeOffset now,
        Guid organizationTypeId = default)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            TextNormalization.CollapseWhitespace(name).Length > 200)
        {
            throw new ArgumentException(
                "Organization name is required and must not exceed 200 characters.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(code) ||
            code.Trim().Length > 50)
        {
            throw new ArgumentException(
                "Organization code is required and must not exceed 50 characters.",
                nameof(code));
        }

        Name = TextNormalization.CollapseWhitespace(name);
        Code = code.Trim().ToUpperInvariant();
        OrganizationTypeId = ResolveTypeId(organizationTypeId);
        UpdatedAt = now;
    }

    private static Guid ResolveTypeId(Guid organizationTypeId) =>
        organizationTypeId == Guid.Empty
            ? WellKnownOrganizationTypes.Uncategorized
            : organizationTypeId;
}
