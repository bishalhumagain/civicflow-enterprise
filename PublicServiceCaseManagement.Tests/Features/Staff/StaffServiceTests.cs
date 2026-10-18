
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Features.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests.Features.Staff;

public sealed class StaffServiceTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid OtherOrganizationId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly DateTimeOffset Now =
        new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private static CivicFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CivicFlowDbContext(options);
    }

    private static StaffMember CreateStaffMember(
        Guid? organizationId = null,
        string? fullName = null,
        string? email = null)
    {
        return StaffMember.Create(
            organizationId ?? OrganizationId,
            fullName ?? "Test Officer",
            email ?? $"officer-{Guid.NewGuid():N}@example.com",
            Now);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyStaffFromRequestedOrganization()
    {
        await using var dbContext = CreateDbContext();

        var organizationStaff = CreateStaffMember();
        var otherOrganizationStaff =
            CreateStaffMember(OtherOrganizationId);

        dbContext.StaffMembers.AddRange(
            organizationStaff,
            otherOrganizationStaff);

        await dbContext.SaveChangesAsync();

        var service = new StaffService(dbContext);

        var result = await service.GetAllAsync(
            OrganizationId,
            null,
            CancellationToken.None);

        var staffMember = Assert.Single(result);
        Assert.Equal(organizationStaff.Id, staffMember.Id);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByActive_ReturnsOnlyActiveStaff()
    {
        await using var dbContext = CreateDbContext();

        var activeStaff = CreateStaffMember();
        var inactiveStaff = CreateStaffMember();
        inactiveStaff.Deactivate(Now);

        dbContext.StaffMembers.AddRange(activeStaff, inactiveStaff);
        await dbContext.SaveChangesAsync();

        var service = new StaffService(dbContext);

        var result = await service.GetAllAsync(
            OrganizationId,
            true,
            CancellationToken.None);

        var staffMember = Assert.Single(result);
        Assert.Equal(activeStaff.Id, staffMember.Id);
        Assert.True(staffMember.IsActive);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByInactive_ReturnsOnlyInactiveStaff()
    {
        await using var dbContext = CreateDbContext();

        var activeStaff = CreateStaffMember();
        var inactiveStaff = CreateStaffMember();
        inactiveStaff.Deactivate(Now);

        dbContext.StaffMembers.AddRange(activeStaff, inactiveStaff);
        await dbContext.SaveChangesAsync();

        var service = new StaffService(dbContext);

        var result = await service.GetAllAsync(
            OrganizationId,
            false,
            CancellationToken.None);

        var staffMember = Assert.Single(result);
        Assert.Equal(inactiveStaff.Id, staffMember.Id);
        Assert.False(staffMember.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenDeactivatingStaff_UpdatesStatus()
    {
        await using var dbContext = CreateDbContext();

        var staffMember = CreateStaffMember();

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = new StaffService(dbContext);

        var result = await service.SetActiveStatusAsync(
            OrganizationId,
            staffMember.Id,
            false,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenReactivatingStaff_UpdatesStatus()
    {
        await using var dbContext = CreateDbContext();

        var staffMember = CreateStaffMember();
        staffMember.Deactivate(Now);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = new StaffService(dbContext);

        var result = await service.SetActiveStatusAsync(
            OrganizationId,
            staffMember.Id,
            true,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenStaffDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var service = new StaffService(dbContext);

        var result = await service.SetActiveStatusAsync(
            OrganizationId,
            Guid.NewGuid(),
            false,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenStaffBelongsToAnotherOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var staffMember = CreateStaffMember(OtherOrganizationId);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = new StaffService(dbContext);

        var result = await service.SetActiveStatusAsync(
            OrganizationId,
            staffMember.Id,
            false,
            CancellationToken.None);

        Assert.Null(result);
        Assert.True(staffMember.IsActive);
    }
}
