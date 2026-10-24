using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Platform;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Features.Platform;

public sealed class PlatformDirectoryServiceTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.UtcNow;

    private static (CivicFlowDbContext Context, PlatformDirectoryService Service)
        CreateService()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"PlatformDirectory_{Guid.NewGuid():N}")
            .Options;

        var context = new CivicFlowDbContext(options);

        return (context, new PlatformDirectoryService(context, TimeProvider.System));
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsAccountsWithTheirOrganizations()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organization = Organization.Create(
            "Kathmandu Metropolitan City",
            "KATHMANDU",
            Now);

        context.Organizations.Add(organization);

        var user = ApplicationUser.Create(
            "Ram Sharma",
            "ram@example.com",
            Now);

        user.PhoneNumber = "+977-9800000000";

        context.ApplicationUsers.Add(user);

        context.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                user.Id,
                organization.Id,
                OrganizationRole.OrganizationAdmin,
                Now));

        await context.SaveChangesAsync();

        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users);

        Assert.Equal("Ram Sharma", summary.FullName);
        Assert.Equal("ram@example.com", summary.Email);
        Assert.Equal("+977-9800000000", summary.PhoneNumber);

        var membership = Assert.Single(summary.Organizations);

        Assert.Equal("KATHMANDU", membership.OrganizationCode);
        Assert.Equal("OrganizationAdmin", membership.Role);
    }

    [Fact]
    public async Task GetStaffAsync_DerivesStaffFromMemberships()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organization = Organization.Create(
            "Pokhara Metropolitan City",
            "POKHARA",
            Now);

        context.Organizations.Add(organization);

        var user = ApplicationUser.Create(
            "Sita Gurung",
            "sita@example.com",
            Now);

        context.ApplicationUsers.Add(user);

        context.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                user.Id,
                organization.Id,
                OrganizationRole.CaseWorker,
                Now));

        await context.SaveChangesAsync();

        var staff = await service.GetStaffAsync();

        var summary = Assert.Single(staff);

        Assert.Equal(user.Id, summary.UserId);
        Assert.Equal("Sita Gurung", summary.FullName);
        Assert.Equal("POKHARA", summary.OrganizationCode);
        Assert.Equal("CaseWorker", summary.Role);
    }

    [Fact]
    public async Task GetOverviewAsync_CountsOrganizationsUsersAndInvitations()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organization = Organization.Create(
            "Biratnagar Metropolitan City",
            "BIRATNAGAR",
            Now);

        context.Organizations.Add(organization);

        var user = ApplicationUser.Create(
            "Gopal Yadav",
            "gopal@example.com",
            Now);

        context.ApplicationUsers.Add(user);

        context.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                user.Id,
                organization.Id,
                OrganizationRole.Viewer,
                Now));

        context.AccountInvitations.Add(
            AccountInvitation.Create(
                organization.Id,
                "pending@example.com",
                "Pending Officer",
                null,
                OrganizationRole.CaseWorker,
                new string('b', 64),
                user.Id,
                Now,
                Now.AddDays(2)));

        await context.SaveChangesAsync();

        var overview = await service.GetOverviewAsync();

        Assert.Equal(1, overview.UserCount);
        Assert.Equal(1, overview.ActiveUserCount);
        Assert.Equal(1, overview.StaffCount);
        Assert.Equal(1, overview.OrganizationCount);
        Assert.Equal(1, overview.ActiveOrganizationCount);
        Assert.Equal(1, overview.InvitationCount);
        Assert.Equal(1, overview.PendingInvitationCount);
    }
}
