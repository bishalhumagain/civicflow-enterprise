using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Api.Features.Platform.Contracts;

using PublicServiceCaseManagement.Api.Domain.Common;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;

namespace PublicServiceCaseManagement.Api.Features.Platform.Services;

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
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    IAuditLogger auditLogger) : IPlatformDirectoryService
{
    public async Task<PagedResponse<PlatformUserSummary>> GetUsersAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        bool? isStaff = null,
        bool? isCitizen = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = TextNormalization.CollapseWhitespace(search).ToLowerInvariant();
        var query = dbContext.ApplicationUsers.AsNoTracking();
        if (term.Length > 0)
        {
            query = query.Where(user =>
                user.FullName.ToLower().Contains(term) ||
                (user.Email ?? string.Empty).ToLower().Contains(term) ||
                (user.PhoneNumber ?? string.Empty).ToLower().Contains(term) ||
                dbContext.OrganizationMemberships.Any(membership =>
                    membership.UserId == user.Id &&
                    (membership.Role.ToString().ToLower().Contains(term) ||
                     dbContext.Organizations.Any(organization =>
                         organization.Id == membership.OrganizationId &&
                         organization.Name.ToLower().Contains(term)))));
        }
        if (isStaff.HasValue)
            query = query.Where(user =>
                dbContext.OrganizationMemberships.Any(membership => membership.UserId == user.Id) == isStaff.Value);
        if (isCitizen.HasValue)
            query = query.Where(user =>
                dbContext.CitizenProfiles.Any(profile => profile.ApplicationUserId == user.Id) == isCitizen.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var sortedQuery = (sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "name_asc" => query.OrderBy(user => user.FullName).ThenBy(user => user.Id),
            "name_desc" => query.OrderByDescending(user => user.FullName).ThenBy(user => user.Id),
            "email_asc" => query.OrderBy(user => user.Email).ThenBy(user => user.Id),
            "email_desc" => query.OrderByDescending(user => user.Email).ThenBy(user => user.Id),
            "created_asc" => query.OrderBy(user => user.CreatedAt).ThenBy(user => user.Id),
            "created_desc" => query.OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id),
            "accounttype_asc" => query.OrderBy(user =>
                dbContext.CitizenProfiles.Any(profile =>
                    profile.ApplicationUserId == user.Id)
                    ? dbContext.OrganizationMemberships.Any(membership =>
                        membership.UserId == user.Id)
                        ? 2
                        : 1
                    : dbContext.OrganizationMemberships.Any(membership =>
                        membership.UserId == user.Id)
                        ? 3
                        : 0).ThenBy(user => user.Id),
            "accounttype_desc" => query.OrderByDescending(user =>
                dbContext.CitizenProfiles.Any(profile =>
                    profile.ApplicationUserId == user.Id)
                    ? dbContext.OrganizationMemberships.Any(membership =>
                        membership.UserId == user.Id)
                        ? 2
                        : 1
                    : dbContext.OrganizationMemberships.Any(membership =>
                        membership.UserId == user.Id)
                        ? 3
                        : 0).ThenBy(user => user.Id),
            "organization_asc" => query.OrderBy(user =>
                dbContext.OrganizationMemberships
                    .Where(membership => membership.UserId == user.Id)
                    .Join(
                        dbContext.Organizations,
                        membership => membership.OrganizationId,
                        organization => organization.Id,
                        (_, organization) => organization.Name)
                    .OrderBy(name => name)
                    .FirstOrDefault()).ThenBy(user => user.Id),
            "organization_desc" => query.OrderByDescending(user =>
                dbContext.OrganizationMemberships
                    .Where(membership => membership.UserId == user.Id)
                    .Join(
                        dbContext.Organizations,
                        membership => membership.OrganizationId,
                        organization => organization.Id,
                        (_, organization) => organization.Name)
                    .OrderBy(name => name)
                    .FirstOrDefault()).ThenBy(user => user.Id),
            _ => query.OrderBy(user => user.FullName).ThenBy(user => user.Id)
        };
        var users = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return new PagedResponse<PlatformUserSummary>(
                Array.Empty<PlatformUserSummary>(), page, pageSize, totalCount,
                (int)Math.Ceiling(totalCount / (double)pageSize));
        }

        var userIds = users.Select(user => user.Id).ToList();

        var memberships = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => userIds.Contains(membership.UserId))
            .ToListAsync(cancellationToken);

        var organizationIds = memberships
            .Select(membership => membership.OrganizationId)
            .Distinct()
            .ToList();
        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organizationIds.Contains(organization.Id))
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization,
                cancellationToken);

        var roleNames = await LoadRoleNamesAsync(userIds, cancellationToken);

        var citizenUserIds = await dbContext.CitizenProfiles
            .AsNoTracking()
            .Where(profile => userIds.Contains(profile.ApplicationUserId))
            .Select(profile => profile.ApplicationUserId)
            .ToListAsync(cancellationToken);

        var citizenSet = citizenUserIds.ToHashSet();

        var items = users
            .Select(user => new PlatformUserSummary(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                string.IsNullOrWhiteSpace(user.PhoneNumber)
                    ? null
                    : user.PhoneNumber,
                user.EmailConfirmed,
                user.IsActive,
                citizenSet.Contains(user.Id),
                memberships.Any(membership => membership.UserId == user.Id) ||
                    (roleNames.TryGetValue(user.Id, out var userRoles) &&
                     userRoles.Contains(AppRoles.Staff, StringComparer.OrdinalIgnoreCase)),
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
        return new PagedResponse<PlatformUserSummary>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<PagedResponse<PlatformStaffSummary>> GetStaffAsync(
        int page = 1,
        int pageSize = 25,
        Guid? organizationId = null,
        bool? isActive = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = TextNormalization.CollapseWhitespace(search).ToLowerInvariant();
        var query =
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join user in dbContext.ApplicationUsers.AsNoTracking()
                on membership.UserId equals user.Id
            join organization in dbContext.Organizations.AsNoTracking()
                on membership.OrganizationId equals organization.Id
            select new { Membership = membership, User = user, Organization = organization };
        if (organizationId.HasValue)
            query = query.Where(row => row.Membership.OrganizationId == organizationId.Value);
        if (isActive.HasValue)
            query = query.Where(row => row.Membership.IsActive == isActive.Value);
        if (term.Length > 0)
            query = query.Where(row =>
                row.User.FullName.ToLower().Contains(term) ||
                (row.User.Email ?? string.Empty).ToLower().Contains(term) ||
                row.Organization.Name.ToLower().Contains(term) ||
                row.Organization.Code.ToLower().Contains(term));

        var totalCount = await query.CountAsync(cancellationToken);
        var memberships = await query
            .OrderBy(row => row.Organization.Name)
            .ThenBy(row => row.User.FullName)
            .ThenBy(row => row.Membership.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => row.Membership)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return new PagedResponse<PlatformStaffSummary>(
                Array.Empty<PlatformStaffSummary>(), page, pageSize, totalCount,
                (int)Math.Ceiling(totalCount / (double)pageSize));
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

        var organizationIds = memberships.Select(membership => membership.OrganizationId)
            .Distinct().ToList();
        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organizationIds.Contains(organization.Id))
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization,
                cancellationToken);

        var items = memberships
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
        return new PagedResponse<PlatformStaffSummary>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
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

    public async Task<PagedResponse<PlatformCitizenSummary>> GetCitizensAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? sort = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = TextNormalization.CollapseWhitespace(search).ToLowerInvariant();
        var query =
            from profile in dbContext.CitizenProfiles.AsNoTracking()
            join user in dbContext.ApplicationUsers.AsNoTracking()
                on profile.ApplicationUserId equals user.Id
            select new { Profile = profile, User = user };
        if (term.Length > 0)
        {
            query = query.Where(row =>
                row.Profile.FullName.ToLower().Contains(term) ||
                row.Profile.Email.ToLower().Contains(term) ||
                (row.Profile.NIDNumber ?? string.Empty).ToLower().Contains(term) ||
                (row.Profile.CitizenshipNumber ?? string.Empty).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var sortedQuery = (sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "name_asc" => query.OrderBy(row => row.Profile.FullName).ThenBy(row => row.Profile.Id),
            "name_desc" => query.OrderByDescending(row => row.Profile.FullName).ThenBy(row => row.Profile.Id),
            "email_asc" => query.OrderBy(row => row.Profile.Email).ThenBy(row => row.Profile.Id),
            "email_desc" => query.OrderByDescending(row => row.Profile.Email).ThenBy(row => row.Profile.Id),
            "created_asc" => query.OrderBy(row => row.Profile.CreatedAt).ThenBy(row => row.Profile.Id),
            "created_desc" => query.OrderByDescending(row => row.Profile.CreatedAt).ThenBy(row => row.Profile.Id),
            _ => query.OrderBy(row => row.Profile.FullName).ThenBy(row => row.Profile.Id)
        };
        var rows = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => ToCitizenSummary(row.User, row.Profile))
            .ToList();
        return new PagedResponse<PlatformCitizenSummary>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<PlatformUserSummary> UpdateStaffAccountAsync(
        Guid userId,
        UpdatePlatformStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("The staff account does not exist.");
        }

        var isStaff = await dbContext.OrganizationMemberships
            .AnyAsync(membership => membership.UserId == userId, cancellationToken);

        if (!isStaff)
        {
            throw new InvalidOperationException(
                "The account is not associated with any organization membership.");
        }

        var fullName = TextNormalization.NormalizeName(
            request.FullName,
            nameof(request.FullName));

        var phoneNumber = PhoneNumbers.Normalize(
            request.PhoneNumber,
            nameof(request.PhoneNumber));

        var now = timeProvider.GetUtcNow();

        user.FullName = fullName;
        user.PhoneNumber = phoneNumber;
        user.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountProfileUpdated,
                EntityType = "ApplicationUser",
                EntityId = user.Id.ToString(),
                Details = $"Staff account {user.Email} updated by a platform administrator."
            },
            cancellationToken);

        var summary = (await GetUsersAsync(
                1, 100, user.Email, cancellationToken: cancellationToken))
            .Items
            .Single(item => item.Id == user.Id);

        return summary;
    }

    public async Task<PlatformCitizenSummary> UpdateCitizenAsync(
        Guid userId,
        UpdatePlatformCitizenRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("The citizen account does not exist.");
        }

        var profile = await dbContext.CitizenProfiles
            .SingleOrDefaultAsync(
                candidate => candidate.ApplicationUserId == userId,
                cancellationToken);

        if (profile is null)
        {
            throw new KeyNotFoundException("The citizen profile does not exist.");
        }

        var fullName = TextNormalization.NormalizeName(
            request.FullName,
            nameof(request.FullName));

        if (request.Age is < 0 or > 150)
        {
            throw new ArgumentException(
                "Age must be between 0 and 150.",
                nameof(request.Age));
        }

        var nid = TextNormalization.NormalizeOptionalIdentifier(
            request.NIDNumber);
        if (!string.IsNullOrWhiteSpace(nid) &&
            await dbContext.CitizenProfiles.AnyAsync(
                candidate => candidate.Id != profile.Id &&
                             candidate.NIDNumber == nid,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "A citizen profile with this NID number already exists.");
        }

        var citizenship = TextNormalization.NormalizeOptionalIdentifier(
            request.CitizenshipNumber);
        if (!string.IsNullOrWhiteSpace(citizenship) &&
            await dbContext.CitizenProfiles.AnyAsync(
                candidate => candidate.Id != profile.Id &&
                             candidate.CitizenshipNumber == citizenship,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "A citizen profile with this citizenship number already exists.");
        }

        var now = timeProvider.GetUtcNow();

        profile.Update(
            fullName,
            request.PhoneNumber,
            nid,
            citizenship,
            request.Age,
            request.Gender,
            request.PermanentAddress,
            request.TemporaryAddress,
            now);

        user.FullName = profile.FullName;
        user.PhoneNumber = profile.PhoneNumber;
        user.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountProfileUpdated,
                EntityType = "CitizenProfile",
                EntityId = profile.Id.ToString(),
                Details = $"Citizen profile for {profile.Email} updated by a platform administrator."
            },
            cancellationToken);

        return ToCitizenSummary(user, profile);
    }

    public async Task SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException($"User with ID {userId} not found.");

        user.IsActive = isActive;
        user.UpdatedAt = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        // Rotating the security stamp invalidates any session cookie the
        // account is still holding, so deactivating an account takes effect
        // immediately rather than only blocking new sign-ins.
        await userManager.UpdateSecurityStampAsync(user);
    }

    public async Task ChangePlatformRoleAsync(
        Guid userId,
        string platformRole,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PlatformRole>(platformRole, true, out var role) ||
            !Enum.IsDefined(role))
        {
            throw new ArgumentException($"Invalid platform role: {platformRole}");
        }

        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
            throw new KeyNotFoundException($"User with ID {userId} not found.");

        var previousRole = user.PlatformRole;
        var hasIdentityRole = await userManager.IsInRoleAsync(
            user,
            AppRoles.SuperAdmin);
        var shouldHaveIdentityRole = role == PlatformRole.SuperAdmin;
        var changed = previousRole != role ||
                      hasIdentityRole != shouldHaveIdentityRole;

        if (!changed)
            return;

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (hasIdentityRole != shouldHaveIdentityRole)
        {
            var result = shouldHaveIdentityRole
                ? await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin)
                : await userManager.RemoveFromRoleAsync(user, AppRoles.SuperAdmin);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to synchronize the platform administrator role: " +
                    string.Join(
                        " ",
                        result.Errors.Select(error => error.Description)));
            }
        }

        user.SetPlatformRole(role, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        // A privilege change must not leave a stale session in place: rotating
        // the stamp forces both cookie and bearer credentials to be re-evaluated.
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Unable to rotate the security stamp after changing platform role.");
        }

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.PlatformRoleChanged,
                EntityType = "Account",
                EntityId = user.Id.ToString(),
                Details = $"platform_role: {previousRole} -> {role}"
            },
            cancellationToken);
    }

    private static PlatformCitizenSummary ToCitizenSummary(
        ApplicationUser user,
        CitizenProfile profile) =>
        new(
            user.Id,
            profile.Id,
            profile.FullName,
            profile.Email,
            profile.PhoneNumber,
            user.EmailConfirmed,
            user.IsActive,
            profile.NIDNumber,
            profile.CitizenshipNumber,
            profile.Age,
            profile.Gender,
            profile.PermanentAddress,
            profile.TemporaryAddress,
            profile.CreatedAt,
            profile.UpdatedAt);

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
