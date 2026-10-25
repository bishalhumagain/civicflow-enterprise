namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Code,
    Guid OrganizationTypeId,
    string? OrganizationTypeName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
