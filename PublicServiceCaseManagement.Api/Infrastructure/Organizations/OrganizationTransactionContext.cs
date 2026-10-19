using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class OrganizationTransactionContext(
    CivicFlowDbContext dbContext) : IOrganizationTransactionContext
{
    private IDbContextTransaction? transaction;
    private Guid? organizationId;

    public bool IsActive => transaction is not null;

    public async Task BeginAsync(
        Guid requestedOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (requestedOrganizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid organization is required.",
                nameof(requestedOrganizationId));
        }

        if (organizationId.HasValue && organizationId != requestedOrganizationId)
        {
            throw new InvalidOperationException(
                "A request cannot switch organization context after it has been resolved.");
        }

        if (!dbContext.Database.IsNpgsql())
        {
            organizationId = requestedOrganizationId;
            return;
        }

        if (organizationId == requestedOrganizationId && transaction is not null)
        {
            return;
        }

        if (transaction is not null)
        {
            throw new InvalidOperationException(
                "A request cannot switch organization context after a transaction has started.");
        }

        transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        organizationId = requestedOrganizationId;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"select set_config('civicflow.organization_id', {requestedOrganizationId.ToString()}, true)",
            cancellationToken);
    }

    public async Task CompleteAsync(
        bool succeeded,
        CancellationToken cancellationToken = default)
    {
        if (transaction is null)
        {
            return;
        }

        try
        {
            if (succeeded)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            await transaction.DisposeAsync();
            transaction = null;
            organizationId = null;
        }
    }
}
