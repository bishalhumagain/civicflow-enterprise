using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Memberships.Contracts;

public sealed record ChangeOrganizationMembershipRoleRequest(
    [Required]
    [NotWhiteSpace]
    [StringLength(32)]
    string Role);