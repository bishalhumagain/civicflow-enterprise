namespace PublicServiceCaseManagement.Api.Features.Memberships;

public sealed record CreateOrganizationMembershipRequest(
    Guid UserId,
    string Role);