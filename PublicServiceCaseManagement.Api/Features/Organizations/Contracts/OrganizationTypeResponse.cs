using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

public sealed record OrganizationTypeResponse(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int OrganizationCount);
