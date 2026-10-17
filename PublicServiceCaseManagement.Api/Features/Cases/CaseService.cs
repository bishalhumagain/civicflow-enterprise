using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CaseService(CivicFlowDbContext dbContext) : ICaseService
{
    public async Task<Case> CreateAsync(
        Guid organizationId,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var referenceNumber = $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant();

        var newCase = Case.Create(
            organizationId,
            referenceNumber,
            title,
            description,
            now);

        dbContext.Cases.Add(newCase);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newCase;
    }

    public async Task<Case?> GetByIdAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id && c.OrganizationId == organizationId,
                cancellationToken);
    }
    
    public async Task<(IReadOnlyList<Case> Items, int TotalCount)> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Cases
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}