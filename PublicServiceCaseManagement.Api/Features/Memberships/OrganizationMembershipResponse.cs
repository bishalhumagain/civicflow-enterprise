namespace PublicServiceCaseManagement.Api.Features.Memberships;

public sealed record OrganizationMembershipResponse(
    Guid Id,
    Guid UserId,
    Guid OrganizationId,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);