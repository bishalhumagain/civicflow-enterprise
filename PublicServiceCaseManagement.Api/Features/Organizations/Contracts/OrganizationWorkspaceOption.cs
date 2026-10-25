namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

public sealed record OrganizationWorkspaceOption(
    Guid Id,
    string Name,
    string Code,
    string Role);
