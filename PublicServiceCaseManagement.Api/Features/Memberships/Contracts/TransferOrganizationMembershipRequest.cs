using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Memberships.Contracts;

/// <summary>
/// Moves a single membership to another organization. The source membership
/// is deactivated and the destination membership is created or reactivated;
/// memberships in unrelated organizations are never touched.
/// </summary>
public sealed record TransferOrganizationMembershipRequest(
    [NotEmptyGuid]
    Guid DestinationOrganizationId,
    [Required]
    [NotWhiteSpace]
    [StringLength(32)]
    string Role);
