
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public sealed class StaffService(CivicFlowDbContext dbContext)
    : IStaffService
{
    public async Task<StaffMember> CreateAsync(
        Guid organizationId,
        string fullName,
        string email,
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
            DateTimeOffset.UtcNow);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        return staffMember;
    }
}
