
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Memberships.Services;

public sealed class OrganizationMembershipService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger) : IOrganizationMembershipService
{

    public async Task<OrganizationMembership> CreateAsync(
        Guid userId,
        Guid organizationId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var userExists = await dbContext.ApplicationUsers
            .AnyAsync(
                user => user.Id == userId,
                cancellationToken);

        if (!userExists)
        {
            throw new KeyNotFoundException(
                "The application user does not exist.");
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(
                item => item.Id == organizationId,
                cancellationToken);

        if (organization is null)
        {
            throw new KeyNotFoundException(
                "The organization does not exist.");
        }

        if (!organization.IsActive)
        {
            throw new InvalidOperationException(
                "Cannot create a membership in an inactive organization.");
        }

        var membershipExists = await dbContext.OrganizationMemberships
            .AnyAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.OrganizationId == organizationId,
                cancellationToken);

        if (membershipExists)
        {
            throw new InvalidOperationException(
                "This user already has a membership in the organization.");
        }

        var membership = OrganizationMembership.Create(
            userId,
            organizationId,
            role,
            DateTimeOffset.UtcNow);

        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Membership,
                Action = AuditActions.MembershipCreated,
                OrganizationId = organizationId,
                EntityType = "OrganizationMembership",
                EntityId = membership.Id.ToString(),
                Details = $"userId={userId} role={membership.Role}"
            },
            cancellationToken);

        return membership;
    }

    public async Task<IReadOnlyList<OrganizationMembership>> GetAllAsync(
        Guid organizationId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(
                membership =>
                    membership.OrganizationId == organizationId);

        if (isActive.HasValue)
        {
            query = query.Where(
                membership => membership.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationMembership?> GetByIdAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        return await dbContext.OrganizationMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                membership =>
                    membership.Id == membershipId &&
                    membership.OrganizationId == organizationId,
                cancellationToken);
    }

    public async Task<OrganizationMembership?> ChangeRoleAsync(
        Guid organizationId,
        Guid membershipId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships
            .SingleOrDefaultAsync(
                item =>
                    item.Id == membershipId &&
                    item.OrganizationId == organizationId,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var previousRole = membership.Role;

        membership.ChangeRole(role, DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Membership,
                Action = AuditActions.MembershipRoleChanged,
                OrganizationId = organizationId,
                EntityType = "OrganizationMembership",
                EntityId = membership.Id.ToString(),
                Details =
                    $"userId={membership.UserId} " +
                    $"role {previousRole} -> {membership.Role}"
            },
            cancellationToken);

        return membership;
    }

    public async Task<OrganizationMembership> TransferAsync(
        Guid sourceOrganizationId,
        Guid membershipId,
        Guid destinationOrganizationId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        if (sourceOrganizationId == destinationOrganizationId)
        {
            throw new ArgumentException(
                "The destination organization must be different from the source organization.",
                nameof(destinationOrganizationId));
        }

        var membership = await dbContext.OrganizationMemberships
            .SingleOrDefaultAsync(
                item =>
                    item.Id == membershipId &&
                    item.OrganizationId == sourceOrganizationId,
                cancellationToken);

        if (membership is null)
        {
            throw new KeyNotFoundException("The membership does not exist.");
        }

        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                candidate => candidate.Id == membership.UserId,
                cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("The application user does not exist.");
        }

        var destination = await dbContext.Organizations
            .SingleOrDefaultAsync(
                candidate => candidate.Id == destinationOrganizationId,
                cancellationToken);

        if (destination is null)
        {
            throw new KeyNotFoundException("The destination organization does not exist.");
        }

        if (!destination.IsActive)
        {
            throw new InvalidOperationException(
                "Cannot transfer a membership to an inactive organization.");
        }

        var destinationExists = await dbContext.OrganizationMemberships
            .AnyAsync(
                candidate =>
                    candidate.UserId == membership.UserId &&
                    candidate.OrganizationId == destinationOrganizationId,
                cancellationToken);

        if (destinationExists)
        {
            throw new InvalidOperationException(
                "This user already has a membership in the destination organization.");
        }

        var now = DateTimeOffset.UtcNow;
        var previousRole = membership.Role;
        var previousOrganizationId = membership.OrganizationId;

        membership.Deactivate(now);

        var transferred = OrganizationMembership.Create(
            membership.UserId,
            destinationOrganizationId,
            role,
            now);

        dbContext.OrganizationMemberships.Add(transferred);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Membership,
                Action = AuditActions.MembershipTransferred,
                OrganizationId = destinationOrganizationId,
                EntityType = "OrganizationMembership",
                EntityId = transferred.Id.ToString(),
                Details =
                    $"userId={membership.UserId} organization " +
                    $"{previousOrganizationId} -> {destinationOrganizationId} " +
                    $"role {previousRole} -> {role}"
            },
            cancellationToken);

        return transferred;
    }

    public async Task<OrganizationMembership?> SetActiveStatusAsync(
        Guid organizationId,
        Guid membershipId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships
            .SingleOrDefaultAsync(
                item =>
                    item.Id == membershipId &&
                    item.OrganizationId == organizationId,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (isActive)
        {
            membership.Reactivate(now);
        }
        else
        {
            membership.Deactivate(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Membership,
                Action = isActive
                    ? AuditActions.MembershipActivated
                    : AuditActions.MembershipDeactivated,
                OrganizationId = organizationId,
                EntityType = "OrganizationMembership",
                EntityId = membership.Id.ToString(),
                Details = $"userId={membership.UserId} role={membership.Role}"
            },
            cancellationToken);

        return membership;
    }
}
