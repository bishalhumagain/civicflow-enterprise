namespace PublicServiceCaseManagement.Api.Features.Organizations;

public sealed record OrganizationWorkspaceOption(
    Guid Id,
    string Name,
    string Code,
    string Role);
