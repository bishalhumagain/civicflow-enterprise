namespace PublicServiceCaseManagement.Api.Domain.Organizations;

public sealed class OrganizationMembership
{
    private OrganizationMembership()
    {
    }

    private OrganizationMembership(
        Guid userId,
        Guid organizationId,
        OrganizationRole role,
        DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        OrganizationId = organizationId;
        Role = role;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public OrganizationRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static OrganizationMembership Create(
        Guid userId,
        Guid organizationId,
        OrganizationRole role,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization ID is required.",
                nameof(organizationId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                "The organization role is invalid.");
        }

        return new OrganizationMembership(
            userId,
            organizationId,
            role,
            now);
    }

    public void ChangeRole(
        OrganizationRole role,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                "The organization role is invalid.");
        }

        if (Role == role)
        {
            return;
        }

        Role = role;
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
