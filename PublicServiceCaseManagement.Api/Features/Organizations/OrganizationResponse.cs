namespace PublicServiceCaseManagement.Api.Features.Organizations;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);