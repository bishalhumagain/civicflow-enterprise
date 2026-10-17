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
}