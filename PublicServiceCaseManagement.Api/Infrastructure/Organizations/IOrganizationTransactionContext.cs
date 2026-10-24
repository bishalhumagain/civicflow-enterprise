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

    Task CompleteAsync(
        bool succeeded,
        CancellationToken cancellationToken = default);
}
