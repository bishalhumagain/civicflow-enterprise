using Microsoft.AspNetCore.Identity;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Staff;

/// <summary>
/// Provisions staff login accounts. Only staff-level identities can be
/// created here; platform super-administrator accounts are never created
/// through this path, which keeps privilege escalation out of reach of
/// organization administrators.
/// </summary>
public sealed class StaffAccountService(
    CivicFlowDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : IStaffAccountService
{
    public async Task<ProvisionStaffAccountResponse> ProvisionAsync(
        Guid organizationId,
        ProvisionStaffAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var organizationRole = ParseOrganizationRole(request.Role);
        var email = request.Email.Trim().ToLowerInvariant();

        var membership = await dbContext.Organizations
            .FindAsync([organizationId], cancellationToken);

        if (membership is null || !membership.IsActive)
        {
            throw new KeyNotFoundException("The organization does not exist.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException(
                "An account already exists for this email address.");
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = ApplicationUser.Create(
            request.FullName,
            email,
            now);

        var createResult = await userManager.CreateAsync(
            user,
            request.TemporaryPassword);

        if (!createResult.Succeeded)
        {
            var message = string.Join(
                " ",
                createResult.Errors
                    .Select(error => error.Description)
                    .Distinct(StringComparer.Ordinal));

            throw new InvalidOperationException(message);
        }

        await userManager.AddToRoleAsync(user, AppRoles.Staff);

        var organizationMembership = OrganizationMembership.Create(
            user.Id,
            organizationId,
            organizationRole,
            now);

        var staffMember = StaffMember.Create(
            organizationId,
            request.FullName,
            email,
            now);

        dbContext.OrganizationMemberships.Add(organizationMembership);
        dbContext.StaffMembers.Add(staffMember);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        auditLogger.AccountLifecycle(
            user.Email ?? email,
            user.Id,
            "staff_provisioned");

        auditLogger.PrivilegeChange(
            user.Id,
            $"role=staff organizationRole={organizationRole} " +
            $"organizationId={organizationId}");

        return new ProvisionStaffAccountResponse(
            user.Id,
            organizationMembership.Id,
            staffMember.Id,
            user.FullName,
            user.Email ?? email,
            AppRoles.Staff,
            organizationRole);
    }

    private static OrganizationRole ParseOrganizationRole(string role)
    {
        if (!Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            throw new ArgumentException(
                "The role must be one of: Viewer, CaseWorker, OrganizationAdmin.",
                nameof(role));
        }

        return parsed;
    }
}
