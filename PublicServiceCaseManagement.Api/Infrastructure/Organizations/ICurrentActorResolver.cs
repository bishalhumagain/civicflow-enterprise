namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

/// <summary>
/// The account that is performing the current request.
/// </summary>
public sealed record CurrentActor(Guid UserId, string Email);

/// <summary>
/// Resolves the acting account so administrative actions can be attributed
/// in the audit trail without every controller resolving it by hand.
/// </summary>
public interface ICurrentActorResolver
{
    Task<CurrentActor?> ResolveAsync(
        CancellationToken cancellationToken = default);
}
