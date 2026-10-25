using PublicServiceCaseManagement.Api.Features.Profiles.Contracts;

namespace PublicServiceCaseManagement.Api.Features.Profiles.Services;

public interface IProfileService
{
    /// <summary>
    /// Determines whether the account is served as a citizen profile or as an
    /// organization staff profile.
    /// </summary>
    Task<ProfileAccountType> GetAccountTypeAsync(
        Guid userId,
        IReadOnlyList<string> roleClaims,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the organization whose staff record backs the profile. The
    /// preferred organization (the one the caller asked for) wins when the
    /// account is a member of it, otherwise the first active membership is
    /// used.
    /// </summary>
    Task<Guid?> ResolveOrganizationIdAsync(
        Guid userId,
        Guid? preferredOrganizationId,
        CancellationToken cancellationToken = default);

    Task<ProfileResponse?> GetAsync(
        ProfileContext context,
        CancellationToken cancellationToken = default);

    Task<ProfileResponse?> UpdateAsync(
        ProfileContext context,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}
