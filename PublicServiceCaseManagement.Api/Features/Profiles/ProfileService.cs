using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Profiles;

public sealed class ProfileService(
    CivicFlowDbContext dbContext,
    TimeProvider timeProvider,
    IAuditLogger auditLogger) : IProfileService
{
    public async Task<ProfileAccountType> GetAccountTypeAsync(
        Guid userId,
        IReadOnlyList<string> roleClaims,
        CancellationToken cancellationToken = default)
    {
        if (HasRole(roleClaims, AppRoles.Citizen))
        {
            return ProfileAccountType.Citizen;
        }

        if (HasRole(roleClaims, AppRoles.Staff))
        {
            return ProfileAccountType.Staff;
        }

        var hasCitizenProfile = await dbContext.CitizenProfiles
            .AsNoTracking()
            .AnyAsync(
                profile => profile.ApplicationUserId == userId,
                cancellationToken);

        if (hasCitizenProfile)
        {
            return ProfileAccountType.Citizen;
        }

        var hasMembership = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.UserId == userId && membership.IsActive,
                cancellationToken);

        return hasMembership ? ProfileAccountType.Staff : ProfileAccountType.Citizen;
    }

    public async Task<Guid?> ResolveOrganizationIdAsync(
        Guid userId,
        Guid? preferredOrganizationId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await (
            from membership in dbContext.OrganizationMemberships.AsNoTracking()
            join organization in dbContext.Organizations.AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.UserId == userId &&
                  membership.IsActive &&
                  organization.IsActive
            orderby membership.CreatedAt
            select membership.OrganizationId)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return null;
        }

        if (preferredOrganizationId.HasValue &&
            memberships.Contains(preferredOrganizationId.Value))
        {
            return preferredOrganizationId.Value;
        }

        return memberships[0];
    }

    public async Task<ProfileResponse?> GetAsync(
        ProfileContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == context.UserId,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var citizenProfile = context.AccountType == ProfileAccountType.Citizen
            ? await dbContext.CitizenProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    profile => profile.ApplicationUserId == user.Id,
                    cancellationToken)
            : null;

        var staffMember = context.AccountType == ProfileAccountType.Staff
            ? await FindStaffMemberAsync(user, context.OrganizationId, cancellationToken)
            : null;

        var organizationName = await ResolveOrganizationNameAsync(
            context.OrganizationId,
            cancellationToken);

        var organizationRole = await ResolveOrganizationRoleAsync(
            context,
            cancellationToken);

        return new ProfileResponse(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            user.EmailConfirmed,
            citizenProfile?.PhoneNumber ?? staffMember?.PhoneNumber,
            context.AccountType,
            context.Roles,
            context.OrganizationId?.ToString(),
            organizationName,
            organizationRole,
            citizenProfile?.NIDNumber,
            citizenProfile?.CitizenshipNumber,
            citizenProfile?.Age,
            citizenProfile?.Gender,
            citizenProfile?.PermanentAddress,
            citizenProfile?.TemporaryAddress,
            citizenProfile?.UpdatedAt ?? user.UpdatedAt);
    }

    public async Task<ProfileResponse?> UpdateAsync(
        ProfileContext context,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                candidate => candidate.Id == context.UserId,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var fullName = NormalizeFullName(request.FullName);
        var phoneNumber = PhoneNumbers.Normalize(request.PhoneNumber);
        var now = timeProvider.GetUtcNow();

        var changedFields = new List<string>();

        if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
        {
            user.FullName = fullName;
            changedFields.Add("full name");
        }

        user.UpdatedAt = now;

        if (context.AccountType == ProfileAccountType.Citizen)
        {
            await UpdateCitizenProfileAsync(
                user,
                fullName,
                phoneNumber,
                request,
                changedFields,
                now,
                cancellationToken);
        }
        else
        {
            await UpdateStaffProfileAsync(
                user,
                context,
                fullName,
                phoneNumber,
                changedFields,
                now,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Account,
                Action = AuditActions.AccountProfileUpdated,
                ActorUserId = user.Id,
                OrganizationId = context.OrganizationId,
                EntityType = "Profile",
                EntityId = user.Id.ToString(),
                Details = changedFields.Count == 0
                    ? "The profile was submitted without any change."
                    : $"Updated {string.Join(", ", changedFields)}."
            },
            cancellationToken);

        return await GetAsync(context, cancellationToken);
    }

    private async Task UpdateCitizenProfileAsync(
        ApplicationUser user,
        string fullName,
        string? phoneNumber,
        UpdateProfileRequest request,
        List<string> changedFields,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CitizenProfiles
            .SingleOrDefaultAsync(
                candidate => candidate.ApplicationUserId == user.Id,
                cancellationToken);

        var nidNumber = TextNormalization.NormalizeOptionalIdentifier(
            request.NIDNumber);
        var citizenshipNumber = TextNormalization.NormalizeOptionalIdentifier(
            request.CitizenshipNumber);

        if (profile is null)
        {
            // Registration normally creates the row; create-on-update covers
            // accounts that were provisioned without a citizen profile.
            await EnsureIdentifiersAreUniqueAsync(
                null,
                nidNumber,
                citizenshipNumber,
                cancellationToken);

            profile = CitizenProfile.Create(
                user.Id,
                fullName,
                user.Email ?? string.Empty,
                phoneNumber,
                nidNumber,
                citizenshipNumber,
                request.Age,
                request.Gender,
                request.PermanentAddress,
                request.TemporaryAddress,
                now);

            dbContext.CitizenProfiles.Add(profile);
            changedFields.Add("citizen details");
            return;
        }

        await EnsureIdentifiersAreUniqueAsync(
            profile,
            nidNumber,
            citizenshipNumber,
            cancellationToken);

        var previousPhoneNumber = profile.PhoneNumber;

        profile.Update(
            fullName,
            phoneNumber,
            nidNumber,
            citizenshipNumber,
            request.Age,
            request.Gender,
            request.PermanentAddress,
            request.TemporaryAddress,
            now);

        if (!string.Equals(previousPhoneNumber, phoneNumber, StringComparison.Ordinal))
        {
            changedFields.Add("phone number");
        }

        changedFields.Add("citizen details");
    }

    private async Task UpdateStaffProfileAsync(
        ApplicationUser user,
        ProfileContext context,
        string fullName,
        string? phoneNumber,
        List<string> changedFields,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!context.OrganizationId.HasValue)
        {
            return;
        }

        var staffMember = await FindStaffMemberAsync(
            user,
            context.OrganizationId,
            cancellationToken,
            tracked: true);

        if (staffMember is null)
        {
            return;
        }

        var previousPhoneNumber = staffMember.PhoneNumber;

        staffMember.UpdateProfile(fullName, phoneNumber, now);

        if (!string.Equals(previousPhoneNumber, phoneNumber, StringComparison.Ordinal))
        {
            changedFields.Add("phone number");
        }

        changedFields.Add("staff details");
    }

    private async Task EnsureIdentifiersAreUniqueAsync(
        CitizenProfile? profile,
        string? nidNumber,
        string? citizenshipNumber,
        CancellationToken cancellationToken)
    {
        var profileId = profile?.Id;

        if (!string.IsNullOrWhiteSpace(nidNumber))
        {
            var nid = nidNumber.Trim();

            if (await dbContext.CitizenProfiles.AnyAsync(
                    candidate => candidate.NIDNumber == nid &&
                                 (!profileId.HasValue || candidate.Id != profileId.Value),
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "A citizen profile with this NID number already exists.");
            }
        }

        if (!string.IsNullOrWhiteSpace(citizenshipNumber))
        {
            var citizenship = citizenshipNumber.Trim();

            if (await dbContext.CitizenProfiles.AnyAsync(
                    candidate => candidate.CitizenshipNumber == citizenship &&
                                 (!profileId.HasValue || candidate.Id != profileId.Value),
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "A citizen profile with this citizenship number already exists.");
            }
        }
    }

    private Task<StaffMember?> FindStaffMemberAsync(
        ApplicationUser user,
        Guid? organizationId,
        CancellationToken cancellationToken,
        bool tracked = false)
    {
        if (!organizationId.HasValue)
        {
            return Task.FromResult<StaffMember?>(null);
        }

        var email = (user.Email ?? string.Empty).ToLowerInvariant();

        var query = tracked
            ? dbContext.StaffMembers
            : dbContext.StaffMembers.AsNoTracking();

        return query.SingleOrDefaultAsync(
            staff => staff.OrganizationId == organizationId.Value &&
                      staff.Email.ToLower() == email,
            cancellationToken);
    }

    private async Task<string?> ResolveOrganizationNameAsync(
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        if (!organizationId.HasValue)
        {
            return null;
        }

        return await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == organizationId.Value)
            .Select(organization => organization.Name)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> ResolveOrganizationRoleAsync(
        ProfileContext context,
        CancellationToken cancellationToken)
    {
        if (!context.OrganizationId.HasValue)
        {
            return null;
        }

        var membership = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == context.UserId &&
                            candidate.OrganizationId == context.OrganizationId.Value,
                cancellationToken);

        return membership?.Role.ToString();
    }

    private static bool HasRole(IReadOnlyList<string> roles, string role) =>
        roles.Any(candidate => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeFullName(string? value) =>
        TextNormalization.NormalizeName(value);
}
