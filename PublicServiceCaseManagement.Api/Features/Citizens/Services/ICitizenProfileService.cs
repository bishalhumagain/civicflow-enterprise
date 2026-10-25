using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;
using System.Threading.Tasks;

namespace PublicServiceCaseManagement.Api.Features.Citizens.Services;

public sealed record CitizenProfileResponse(
    Guid Id,
    Guid ApplicationUserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? NIDNumber,
    string? CitizenshipNumber,
    int? Age,
    string? Gender,
    string? PermanentAddress,
    string? TemporaryAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateCitizenProfileRequest(
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(150, MinimumLength = 1)]
    string FullName,
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [PhoneNumber]
    string? PhoneNumber,
    [StringLength(50)]
    string? NIDNumber,
    [StringLength(50)]
    string? CitizenshipNumber,
    [Range(0, 150)]
    int? Age,
    [StringLength(20)]
    string? Gender,
    [StringLength(500)]
    string? PermanentAddress,
    [StringLength(500)]
    string? TemporaryAddress);

public sealed record UpdateCitizenProfileRequest(
    [PhoneNumber]
    string? PhoneNumber,
    [StringLength(50)]
    string? NIDNumber,
    [StringLength(50)]
    string? CitizenshipNumber,
    [Range(0, 150)]
    int? Age,
    [StringLength(20)]
    string? Gender,
    [StringLength(500)]
    string? PermanentAddress,
    [StringLength(500)]
    string? TemporaryAddress);

public interface ICitizenProfileService
{
    Task<CitizenProfileResponse> CreateAsync(Guid applicationUserId, CreateCitizenProfileRequest request, CancellationToken cancellationToken);
    Task<CitizenProfileResponse?> GetByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<CitizenProfileResponse?> UpdateAsync(Guid applicationUserId, UpdateCitizenProfileRequest request, CancellationToken cancellationToken);
}
