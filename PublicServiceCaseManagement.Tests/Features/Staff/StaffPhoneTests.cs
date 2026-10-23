using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Features.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Tests.Infrastructure;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Features.Staff;

public sealed class StaffPhoneTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static CivicFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CivicFlowDbContext(options);
    }

    [Fact]
    public async Task StaffMemberCreate_StoresThePhoneNumber()
    {
        await using var dbContext = CreateDbContext();

        var staffMember = await AddStaffMemberAsync(
            dbContext,
            "+977-9800000000");

        Assert.Equal("+977-9800000000", staffMember.PhoneNumber);

        var stored = await dbContext.StaffMembers.SingleAsync();

        Assert.Equal("+977-9800000000", stored.PhoneNumber);
    }

    [Fact]
    public async Task StaffMemberCreate_WithoutAPhoneNumber_StoresNoPhoneNumber()
    {
        await using var dbContext = CreateDbContext();

        var staffMember = await AddStaffMemberAsync(dbContext, null);

        Assert.Null(staffMember.PhoneNumber);
    }

    [Fact]
    public void StaffMemberCreate_WithAnInvalidPhoneNumber_Throws()
    {
        Assert.Throws<ArgumentException>(() => StaffMember.Create(
            OrganizationId,
            "Test Officer",
            $"officer-{Guid.NewGuid():N}@example.com",
            "not-a-number",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task UpdatePhoneNumberAsync_ReplacesThePhoneNumber()
    {
        await using var dbContext = CreateDbContext();

        var service = new StaffService(dbContext, new RecordingAuditLogger());

        var staffMember = await AddStaffMemberAsync(
            dbContext,
            "+977-9800000000");

        var updated = await service.UpdatePhoneNumberAsync(
            OrganizationId,
            staffMember.Id,
            "980-1111111",
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("980-1111111", updated.PhoneNumber);
    }

    [Fact]
    public async Task UpdatePhoneNumberAsync_ClearsThePhoneNumberWhenEmpty()
    {
        await using var dbContext = CreateDbContext();

        var service = new StaffService(dbContext, new RecordingAuditLogger());

        var staffMember = await AddStaffMemberAsync(
            dbContext,
            "+977-9800000000");

        var updated = await service.UpdatePhoneNumberAsync(
            OrganizationId,
            staffMember.Id,
            null,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Null(updated.PhoneNumber);
    }

    [Fact]
    public async Task UpdatePhoneNumberAsync_ForAnotherOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var service = new StaffService(dbContext, new RecordingAuditLogger());

        var staffMember = await AddStaffMemberAsync(
            dbContext,
            "+977-9800000000");

        var updated = await service.UpdatePhoneNumberAsync(
            Guid.NewGuid(),
            staffMember.Id,
            "980-1111111",
            CancellationToken.None);

        Assert.Null(updated);
    }

    private static async Task<StaffMember> AddStaffMemberAsync(
        CivicFlowDbContext dbContext,
        string? phoneNumber)
    {
        var staffMember = StaffMember.Create(
            OrganizationId,
            "Test Officer",
            $"officer-{Guid.NewGuid():N}@example.com",
            phoneNumber,
            DateTimeOffset.UtcNow);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        return staffMember;
    }
}