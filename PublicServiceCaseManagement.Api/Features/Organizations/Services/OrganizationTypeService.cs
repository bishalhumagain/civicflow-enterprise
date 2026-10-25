using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

namespace PublicServiceCaseManagement.Api.Features.Organizations.Services;

public sealed class OrganizationTypeService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger) : IOrganizationTypeService
{
    public async Task<OrganizationType> CreateAsync(
        string name,
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);

        var codeExists = await dbContext.OrganizationTypes
            .AnyAsync(
                type => type.Code == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException(
                "An organization type with this code already exists.");
        }

        var organizationType = OrganizationType.Create(
            name,
            normalizedCode,
            DateTimeOffset.UtcNow);

        dbContext.OrganizationTypes.Add(organizationType);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = AuditActions.OrganizationTypeCreated,
                EntityType = "OrganizationType",
                EntityId = organizationType.Id.ToString(),
                Details = $"{organizationType.Name} ({organizationType.Code})"
            },
            cancellationToken);

        return organizationType;
    }

    public async Task<IReadOnlyList<OrganizationTypeResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var results = await dbContext.OrganizationTypes
            .AsNoTracking()
            .Select(type => new
            {
                type.Id,
                type.Name,
                type.Code,
                type.IsActive,
                type.CreatedAt,
                type.UpdatedAt,
                OrganizationCount = dbContext.Organizations
                    .Count(organization =>
                        organization.OrganizationTypeId == type.Id)
            })
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return results
            .Select(item => new OrganizationTypeResponse(
                item.Id,
                item.Name,
                item.Code,
                item.IsActive,
                item.CreatedAt,
                item.UpdatedAt,
                item.OrganizationCount))
            .ToList();
    }

    public async Task<OrganizationType?> GetByIdAsync(
        Guid organizationTypeId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OrganizationTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                type => type.Id == organizationTypeId,
                cancellationToken);
    }

    public async Task<OrganizationTypeResponse?> GetResponseByIdAsync(
        Guid organizationTypeId,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.OrganizationTypes
            .AsNoTracking()
            .Where(type => type.Id == organizationTypeId)
            .Select(type => new
            {
                type.Id,
                type.Name,
                type.Code,
                type.IsActive,
                type.CreatedAt,
                type.UpdatedAt,
                OrganizationCount = dbContext.Organizations
                    .Count(organization =>
                        organization.OrganizationTypeId == type.Id)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (result is null)
            return null;

        return new OrganizationTypeResponse(
            result.Id,
            result.Name,
            result.Code,
            result.IsActive,
            result.CreatedAt,
            result.UpdatedAt,
            result.OrganizationCount);
    }

    public async Task<OrganizationType?> UpdateAsync(
        Guid organizationTypeId,
        string name,
        string code,
        CancellationToken cancellationToken)
    {
        var organizationType = await dbContext.OrganizationTypes
            .SingleOrDefaultAsync(
                type => type.Id == organizationTypeId,
                cancellationToken);

        if (organizationType is null)
            return null;

        var normalizedCode = NormalizeCode(code);

        var codeExists = await dbContext.OrganizationTypes
            .AnyAsync(
                type => type.Id != organizationTypeId &&
                        type.Code == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException(
                "An organization type with this code already exists.");
        }

        var previousName = organizationType.Name;

        organizationType.UpdateDetails(
            name,
            normalizedCode,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = AuditActions.OrganizationTypeUpdated,
                EntityType = "OrganizationType",
                EntityId = organizationType.Id.ToString(),
                Details =
                    $"{previousName} -> {organizationType.Name} " +
                    $"({organizationType.Code})"
            },
            cancellationToken);

        return organizationType;
    }

    public async Task<OrganizationType?> SetActiveStatusAsync(
        Guid organizationTypeId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var organizationType = await dbContext.OrganizationTypes
            .SingleOrDefaultAsync(
                type => type.Id == organizationTypeId,
                cancellationToken);

        if (organizationType is null)
            return null;

        var now = DateTimeOffset.UtcNow;

        if (isActive)
            organizationType.Reactivate(now);
        else
            organizationType.Deactivate(now);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = isActive
                    ? AuditActions.OrganizationTypeActivated
                    : AuditActions.OrganizationTypeDeactivated,
                EntityType = "OrganizationType",
                EntityId = organizationType.Id.ToString(),
                Details = organizationType.Name
            },
            cancellationToken);

        return organizationType;
    }

    public async Task<bool> DeleteAsync(
        Guid organizationTypeId,
        CancellationToken cancellationToken)
    {
        if (organizationTypeId == WellKnownOrganizationTypes.Uncategorized)
        {
            throw new InvalidOperationException(
                "The default uncategorized organization type cannot be deleted.");
        }

        var organizationType = await dbContext.OrganizationTypes
            .SingleOrDefaultAsync(
                type => type.Id == organizationTypeId,
                cancellationToken);

        if (organizationType is null)
            return false;

        var usageCount = await dbContext.Organizations
            .CountAsync(
                organization =>
                    organization.OrganizationTypeId == organizationTypeId,
                cancellationToken);

        if (usageCount > 0)
        {
            throw new InvalidOperationException(
                $"{usageCount} organization{(usageCount == 1 ? string.Empty : "s")} " +
                "still use this type. Reassign them or deactivate the type instead.");
        }

        var name = organizationType.Name;
        var code = organizationType.Code;

        dbContext.OrganizationTypes.Remove(organizationType);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = AuditActions.OrganizationTypeDeleted,
                EntityType = "OrganizationType",
                EntityId = organizationTypeId.ToString(),
                Details = $"{name} ({code})"
            },
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> organizationTypeIds,
        CancellationToken cancellationToken)
    {
        var ids = organizationTypeIds
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.OrganizationTypes
            .AsNoTracking()
            .Where(type => ids.Contains(type.Id))
            .ToDictionaryAsync(
                type => type.Id,
                type => type.Name,
                cancellationToken);
    }

    private static string NormalizeCode(string code) =>
        code?.Trim().ToUpperInvariant() ?? string.Empty;
}
