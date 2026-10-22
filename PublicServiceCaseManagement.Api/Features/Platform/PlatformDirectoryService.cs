using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Platform;

/// <summary>
/// Builds the platform-wide view of accounts and organization staff.
/// </summary>
/// <remarks>
/// The directory is assembled from accounts, memberships, and invitations.
/// The case and staff-record tables are tenant isolated at the database
/// level, so they are deliberately not read here: a platform administrator
/// sees the membership that grants access, which is the authoritative record
/// of who works where.
/// </remarks>
public sealed class PlatformDirectoryService(
    CivicFlowDbContext dbContext,
    TimeProvider timeProvider) : IPlatformDirectoryService
{
    public async Task<IReadOnlyList<PlatformUserSummary>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await dbContext.ApplicationUsers
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return Array.Empty<PlatformUserSummary>();
        }

        var userIds = users.Select(user => user.Id).ToList();

        var memberships = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => userIds.Contains(membership.UserId))
            .ToListAsync(cancellationToken);

        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization,
                cancellationToken);

        var roleNames = await LoadRoleNamesAsync(userIds, cancellationToken);

        return users
            .Select(user => new PlatformUserSummary(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                string.IsNullOrWhiteSpace(user.PhoneNumber)
                    ? null
                    : user.PhoneNumber,
                user.EmailConfirmed,
                user.IsActive,
                user.PlatformRole.ToString(),
                roleNames.TryGetValue(user.Id, out var names)
                    ? names
                    : Array.Empty<string>(),
                user.CreatedAt,
                user.UpdatedAt,
                memberships
                    .Where(membership => membership.UserId == user.Id)
                    .OrderBy(membership =>
                        organizations.TryGetValue(
                            membership.OrganizationId,
                            out var organization)
                            ? organization.Name
                            : string.Empty)
                    .Select(membership => new PlatformUserOrganization(
                        membership.Id,
                        membership.OrganizationId,
                        organizations.TryGetValue(
                            membership.OrganizationId,
                            out var organization)
                            ? organization.Name
                            : "Unknown organization",
                        organizations.TryGetValue(
                            membership.OrganizationId,
                            out var organizationCode)
                            ? organizationCode.Code
                            : string.Empty,
                        membership.Role.ToString(),
                        membership.IsActive))
                    .ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<PlatformStaffSummary>> GetStaffAsync(
        CancellationToken cancellationToken = default)
    {
        var memberships = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return Array.Empty<PlatformStaffSummary>();
        }

        var userIds = memberships
            .Select(membership => membership.UserId)
            .Distinct()
            .ToList();

        var users = await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user,
                cancellationToken);

        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization,
                cancellationToken);

        return memberships
            .Where(membership => users.ContainsKey(membership.UserId))
            .OrderBy(membership =>
                organizations.TryGetValue(
                    membership.OrganizationId,
                    out var organization)
                    ? organization.Name
                    : string.Empty)
            .ThenBy(membership => membership.UserId)
            .Select(membership =>
            {
                var user = users[membership.UserId];

                var organization = organizations.TryGetValue(
                    membership.OrganizationId,
                    out var known)
                    ? known
                    : null;

                return new PlatformStaffSummary(
                    user.Id,
                    membership.Id,
                    membership.OrganizationId,
                    organization?.Name ?? "Unknown organization",
                    organization?.Code ?? string.Empty,
                    user.FullName,
                    user.Email ?? string.Empty,
                    string.IsNullOrWhiteSpace(user.PhoneNumber)
                        ? null
                        : user.PhoneNumber,
                    membership.Role.ToString(),
                    membership.IsActive,
                    user.EmailConfirmed,
                    user.IsActive,
                    membership.CreatedAt);
            })
            .ToList();
    }

    public async Task<PlatformOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var userCount = await dbContext.ApplicationUsers
            .CountAsync(cancellationToken);

        var activeUserCount = await dbContext.ApplicationUsers
            .CountAsync(user => user.IsActive, cancellationToken);

        var membershipCount = await dbContext.OrganizationMemberships
            .CountAsync(cancellationToken);

        var activeMembershipCount = await dbContext.OrganizationMemberships
            .CountAsync(
                membership => membership.IsActive,
                cancellationToken);

        var organizationCount = await dbContext.Organizations
            .CountAsync(cancellationToken);

        var activeOrganizationCount = await dbContext.Organizations
            .CountAsync(
                organization => organization.IsActive,
                cancellationToken);

        var invitationCount = await dbContext.AccountInvitations
            .CountAsync(cancellationToken);

        var pendingInvitationCount = await dbContext.AccountInvitations
            .CountAsync(
                invitation =>
                    invitation.UsedAt == null &&
                    invitation.RevokedAt == null &&
                    invitation.ExpiresAt > now,
                cancellationToken);

        return new PlatformOverviewResponse(
            userCount,
            activeUserCount,
            membershipCount,
            activeMembershipCount,
            organizationCount,
            activeOrganizationCount,
            invitationCount,
            pendingInvitationCount);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>
        LoadRoleNamesAsync(
            IReadOnlyList<Guid> userIds,
            CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .ToDictionaryAsync(
                role => role.Id,
                role => role.Name,
                cancellationToken);

        var userRoles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .ToListAsync(cancellationToken);

        return userRoles
            .Where(userRole => roles.ContainsKey(userRole.RoleId))
            .GroupBy(userRole => userRole.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(userRole => roles[userRole.RoleId])
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList());
    }
}
