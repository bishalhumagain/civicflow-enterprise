using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Memberships;

public sealed record CreateOrganizationMembershipRequest(
    [NotEmptyGuid]
    Guid UserId,
    [Required]
    [NotWhiteSpace]
    [StringLength(32)]
    string Role);