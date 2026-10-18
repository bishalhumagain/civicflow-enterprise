
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

public sealed class OrganizationService(
    CivicFlowDbContext dbContext) : IOrganizationService
{
    public async Task<Organization> CreateAsync(
        string name,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);

        var codeExists = await dbContext.Organizations
            .AnyAsync(
                organization => organization.Code == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException(
                "An organization with this code already exists.");
        }

        var organization = Organization.Create(
            name,
            normalizedCode,
            DateTimeOffset.UtcNow);

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);

        return organization;
    }

    public async Task<IReadOnlyList<Organization>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Organization?> GetByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                organization => organization.Id == organizationId,
                cancellationToken);
    }

    public async Task<Organization?> UpdateAsync(
        Guid organizationId,
        string name,
        string code,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                item => item.Id == organizationId,
                cancellationToken);

        if (organization is null)
            return null;

        var normalizedCode = NormalizeCode(code);

        var codeExists = await dbContext.Organizations
            .AnyAsync(
                item => item.Id != organizationId &&
                        item.Code == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException(
                "An organization with this code already exists.");
        }

        organization.UpdateDetails(
            name,
            normalizedCode,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return organization;
    }

    public async Task<Organization?> SetActiveStatusAsync(
        Guid organizationId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                item => item.Id == organizationId,
                cancellationToken);

        if (organization is null)
            return null;

        var now = DateTimeOffset.UtcNow;

        if (isActive)
            organization.Reactivate(now);
        else
            organization.Deactivate(now);

        await dbContext.SaveChangesAsync(cancellationToken);

        return organization;
    }

    private static string NormalizeCode(string code)
    {
        return code?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
