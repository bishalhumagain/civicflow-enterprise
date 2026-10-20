using System;
using System.Threading;
using System.Threading.Tasks;

namespace PublicServiceCaseManagement.Api.Features.Citizens;

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
    string FullName,
    string Email,
    string? PhoneNumber,
    string? NIDNumber,
    string? CitizenshipNumber,
    int? Age,
    string? Gender,
    string? PermanentAddress,
    string? TemporaryAddress);

public sealed record UpdateCitizenProfileRequest(
    string? PhoneNumber,
    string? NIDNumber,
    string? CitizenshipNumber,
    int? Age,
    string? Gender,
    string? PermanentAddress,
    string? TemporaryAddress);

public interface ICitizenProfileService
{
    Task<CitizenProfileResponse> CreateAsync(Guid applicationUserId, CreateCitizenProfileRequest request, CancellationToken cancellationToken);
    Task<CitizenProfileResponse?> GetByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<CitizenProfileResponse?> UpdateAsync(Guid applicationUserId, UpdateCitizenProfileRequest request, CancellationToken cancellationToken);
}
