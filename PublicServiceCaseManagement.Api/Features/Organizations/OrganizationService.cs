using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

public sealed class OrganizationService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger) : IOrganizationService
{
    public async Task<Organization> CreateAsync(
        string name,
        string code,
        CancellationToken cancellationToken,
        Guid? organizationTypeId = null)
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

        var resolvedTypeId = await ResolveOrganizationTypeIdAsync(
            organizationTypeId,
            cancellationToken);

        var organization = Organization.Create(
            name,
            normalizedCode,
            DateTimeOffset.UtcNow,
            resolvedTypeId);

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = AuditActions.OrganizationCreated,
                OrganizationId = organization.Id,
                EntityType = "Organization",
                EntityId = organization.Id.ToString(),
                Details = $"{organization.Name} ({organization.Code})"
            },
            cancellationToken);

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

    public async Task<IReadOnlyList<CitizenOrganizationOption>>
        GetActiveOrganizationsForCitizensAsync(
            CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.IsActive)
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .Select(organization => new CitizenOrganizationOption(
                organization.Id,
                organization.Name,
                organization.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationWorkspaceOption>>
        GetMembershipsForUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var memberships = await (
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.UserId == userId &&
                  membership.IsActive &&
                  organization.IsActive
            orderby organization.Name, organization.Id
            select new
            {
                organization.Id,
                organization.Name,
                organization.Code,
                membership.Role
            }
        ).ToListAsync(cancellationToken);

        return memberships
            .Select(membership => new OrganizationWorkspaceOption(
                membership.Id,
                membership.Name,
                membership.Code,
                membership.Role.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<Organization>> GetManagedOrganizationsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await (
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.UserId == userId &&
                  membership.IsActive &&
                  membership.Role == OrganizationRole.OrganizationAdmin
            orderby organization.Name, organization.Id
            select organization
        ).ToListAsync(cancellationToken);
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
        CancellationToken cancellationToken,
        Guid? organizationTypeId = null)
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

        var resolvedTypeId = organizationTypeId is null
            ? organization.OrganizationTypeId
            : await ResolveOrganizationTypeIdAsync(
                organizationTypeId,
                cancellationToken);

        var previousName = organization.Name;

        organization.UpdateDetails(
            name,
            normalizedCode,
            DateTimeOffset.UtcNow,
            resolvedTypeId);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = AuditActions.OrganizationUpdated,
                OrganizationId = organization.Id,
                EntityType = "Organization",
                EntityId = organization.Id.ToString(),
                Details =
                    $"{previousName} -> {organization.Name} " +
                    $"({organization.Code})"
            },
            cancellationToken);

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

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = isActive
                    ? AuditActions.OrganizationActivated
                    : AuditActions.OrganizationDeactivated,
                OrganizationId = organization.Id,
                EntityType = "Organization",
                EntityId = organization.Id.ToString(),
                Details = organization.Name
            },
            cancellationToken);

        return organization;
    }

    public async Task<bool> DeleteAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                item => item.Id == organizationId,
                cancellationToken);

        if (organization is null)
            return false;

        var caseCount = await dbContext.Cases
            .CountAsync(
                item => item.OrganizationId == organizationId,
                cancellationToken);

        if (caseCount > 0)
        {
            throw new InvalidOperationException(
                $"The organization still holds {caseCount} " +
                $"case{(caseCount == 1 ? string.Empty : "s")}. " +
                "Deactivate the organization instead of deleting it.");
        }

        var name = organization.Name;
        var code = organization.Code;

        var invitations = await dbContext.AccountInvitations
            .Where(invitation => invitation.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        dbContext.AccountInvitations.RemoveRange(invitations);

        var memberships = await dbContext.OrganizationMemberships
            .Where(membership => membership.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        dbContext.OrganizationMemberships.RemoveRange(memberships);

        var staffMembers = await dbContext.StaffMembers
            .Where(staff => staff.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        dbContext.StaffMembers.RemoveRange(staffMembers);

        dbContext.Organizations.Remove(organization);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Organization,
                Action = AuditActions.OrganizationDeleted,
                OrganizationId = organizationId,
                EntityType = "Organization",
                EntityId = organizationId.ToString(),
                Details =
                    $"{name} ({code}) deleted with " +
                    $"{memberships.Count} membership record(s), " +
                    $"{staffMembers.Count} staff record(s), and " +
                    $"{invitations.Count} invitation(s)."
            },
            cancellationToken);

        return true;
    }

    private async Task<Guid> ResolveOrganizationTypeIdAsync(
        Guid? organizationTypeId,
        CancellationToken cancellationToken)
    {
        if (organizationTypeId is null ||
            organizationTypeId == WellKnownOrganizationTypes.Uncategorized)
        {
            return WellKnownOrganizationTypes.Uncategorized;
        }

        var type = await dbContext.OrganizationTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == organizationTypeId.Value,
                cancellationToken);

        if (type is null)
        {
            throw new ArgumentException(
                "The selected organization type does not exist.",
                nameof(organizationTypeId));
        }

        if (!type.IsActive)
        {
            throw new ArgumentException(
                "The selected organization type is inactive.",
                nameof(organizationTypeId));
        }

        return type.Id;
    }

    private static string NormalizeCode(string code)
    {
        return code?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
