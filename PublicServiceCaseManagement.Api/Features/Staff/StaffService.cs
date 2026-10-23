using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public sealed class StaffService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger)
    : IStaffService
{
    public async Task<StaffMember> CreateAsync(
        Guid organizationId,
        string fullName,
        string email,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim();

        var emailExists = await dbContext.StaffMembers
            .AnyAsync(
                staff => staff.OrganizationId == organizationId &&
                         staff.Email.ToLower() == normalizedEmail.ToLower(),
                cancellationToken);

        if (emailExists)
        {
            throw new InvalidOperationException(
                "A staff member with this email already exists in the organization.");
        }

        var staffMember = StaffMember.Create(
            organizationId,
            fullName,
            normalizedEmail,
            phoneNumber,
            DateTimeOffset.UtcNow);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Staff,
                Action = AuditActions.StaffCreated,
                OrganizationId = organizationId,
                EntityType = "StaffMember",
                EntityId = staffMember.Id.ToString(),
                Details =
                    $"{staffMember.FullName} <{staffMember.Email}>" +
                    (staffMember.PhoneNumber is null
                        ? string.Empty
                        : $" phone={staffMember.PhoneNumber}")
            },
            cancellationToken);

        return staffMember;
    }

    public async Task<IReadOnlyList<StaffMember>> GetAllAsync(
        Guid organizationId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.StaffMembers
            .AsNoTracking()
            .Where(staff => staff.OrganizationId == organizationId);

        if (isActive.HasValue)
        {
            query = query.Where(staff => staff.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(staff => staff.FullName)
            .ThenBy(staff => staff.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StaffMember>> GetAllPlatformStaffAsync(
        Guid? organizationId,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.StaffMembers.AsNoTracking();

        if (organizationId.HasValue)
        {
            query = query.Where(staff => staff.OrganizationId == organizationId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(staff => staff.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(staff => staff.OrganizationId)
            .ThenBy(staff => staff.FullName)
            .ThenBy(staff => staff.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<StaffMember?> SetActiveStatusAsync(
        Guid organizationId,
        Guid staffMemberId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                staff => staff.Id == staffMemberId &&
                         staff.OrganizationId == organizationId,
                cancellationToken);

        if (staffMember is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (isActive)
        {
            staffMember.Reactivate(now);
        }
        else
        {
            staffMember.Deactivate(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Staff,
                Action = isActive
                    ? AuditActions.StaffActivated
                    : AuditActions.StaffDeactivated,
                OrganizationId = organizationId,
                EntityType = "StaffMember",
                EntityId = staffMember.Id.ToString(),
                Details = $"{staffMember.FullName} <{staffMember.Email}>"
            },
            cancellationToken);

        return staffMember;
    }

    public async Task<StaffMember?> UpdatePhoneNumberAsync(
        Guid organizationId,
        Guid staffMemberId,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                staff => staff.Id == staffMemberId &&
                         staff.OrganizationId == organizationId,
                cancellationToken);

        if (staffMember is null)
        {
            return null;
        }

        var previousPhoneNumber = staffMember.PhoneNumber;

        staffMember.UpdatePhoneNumber(
            phoneNumber,
            DateTimeOffset.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Staff,
                Action = AuditActions.StaffPhoneUpdated,
                OrganizationId = organizationId,
                EntityType = "StaffMember",
                EntityId = staffMember.Id.ToString(),
                Details =
                    $"{staffMember.FullName} <{staffMember.Email}> " +
                    $"phone {previousPhoneNumber ?? "(none)"} -> " +
                    $"{staffMember.PhoneNumber ?? "(none)"}"
            },
            cancellationToken);

        return staffMember;
    }
}