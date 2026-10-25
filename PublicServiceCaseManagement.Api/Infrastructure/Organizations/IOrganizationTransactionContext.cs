namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public interface IOrganizationTransactionContext
{
    bool IsActive { get; }

    Task BeginAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task BeginCitizenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a read-only platform context. The database policies only grant
    /// SELECT access for this setting, so a platform administrator can inspect
    /// cases across organizations without gaining a tenant write path.
    /// </summary>
    Task BeginPlatformReadAsync(
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        bool succeeded,
        CancellationToken cancellationToken = default);
}
