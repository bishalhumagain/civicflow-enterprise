namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

/// <summary>Public organization details returned by organization directory routes.</summary>
public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Code,
    Guid OrganizationTypeId,
    string? OrganizationTypeName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);