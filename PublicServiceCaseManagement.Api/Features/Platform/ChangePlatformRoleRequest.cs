using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Platform;

public sealed record ChangePlatformRoleRequest(
    [Required]
    [NotWhiteSpace]
    [StringLength(32)]
    string PlatformRole);
