namespace PublicServiceCaseManagement.Api.Features.Memberships;

/// <summary>
/// Moves a single membership to another organization. The source membership
/// is deactivated and the destination membership is created or reactivated;
/// memberships in unrelated organizations are never touched.
/// </summary>
public sealed record TransferOrganizationMembershipRequest(
    Guid DestinationOrganizationId,
    string Role);
