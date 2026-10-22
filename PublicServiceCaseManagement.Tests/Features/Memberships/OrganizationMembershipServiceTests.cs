
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Memberships;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Tests.Infrastructure;

namespace PublicServiceCaseManagement.Tests.Features.Memberships;

public sealed class OrganizationMembershipServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    private static CivicFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CivicFlowDbContext(options);
    }

    private static Organization CreateOrganization(
        string? code = null)
    {
        return Organization.Create(
            "Test Organization",
            code ?? $"ORG-{Guid.NewGuid():N}",
            Now);
    }

    private static ApplicationUser CreateUser()
    {
        return ApplicationUser.Create(
            "Test User",
            $"user-{Guid.NewGuid():N}@example.com",
            Now);
    }

    private static async Task SeedAsync(
        CivicFlowDbContext dbContext,
        Organization organization,
        ApplicationUser user)
    {
        dbContext.Organizations.Add(organization);
        dbContext.ApplicationUsers.Add(user);

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_CreatesActiveMembership()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.CreateAsync(
            user.Id,
            organization.Id,
            OrganizationRole.CaseWorker,
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(OrganizationRole.CaseWorker, result.Role);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WhenUserDoesNotExist_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateAsync(
                Guid.NewGuid(),
                organization.Id,
                OrganizationRole.Viewer,
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenOrganizationDoesNotExist_ThrowsKeyNotFoundException()
    {
        await using var dbContext = CreateDbContext();

        var user = CreateUser();
        dbContext.ApplicationUsers.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateAsync(
                user.Id,
                Guid.NewGuid(),
                OrganizationRole.Viewer,
                CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WhenMembershipAlreadyExists_ThrowsInvalidOperationException()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        await service.CreateAsync(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                user.Id,
                organization.Id,
                OrganizationRole.CaseWorker,
                CancellationToken.None));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyMembershipsForRequestedOrganization()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var otherOrganization = CreateOrganization();
        var user = CreateUser();
        var otherUser = CreateUser();

        await SeedAsync(dbContext, organization, user);

        dbContext.Organizations.Add(otherOrganization);
        dbContext.ApplicationUsers.Add(otherUser);
        await dbContext.SaveChangesAsync();

        var expected = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        var otherMembership = OrganizationMembership.Create(
            otherUser.Id,
            otherOrganization.Id,
            OrganizationRole.CaseWorker,
            Now);

        dbContext.OrganizationMemberships.AddRange(
            expected,
            otherMembership);

        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.GetAllAsync(
            organization.Id,
            null,
            CancellationToken.None);

        var membership = Assert.Single(result);
        Assert.Equal(expected.Id, membership.Id);
    }

    [Fact]
    public async Task GetAllAsync_WhenFilteredByActiveStatus_ReturnsMatchingMemberships()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var activeUser = CreateUser();
        var inactiveUser = CreateUser();

        await SeedAsync(dbContext, organization, activeUser);

        dbContext.ApplicationUsers.Add(inactiveUser);
        await dbContext.SaveChangesAsync();

        var activeMembership = OrganizationMembership.Create(
            activeUser.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        var inactiveMembership = OrganizationMembership.Create(
            inactiveUser.Id,
            organization.Id,
            OrganizationRole.CaseWorker,
            Now);

        inactiveMembership.Deactivate(Now);

        dbContext.OrganizationMemberships.AddRange(
            activeMembership,
            inactiveMembership);

        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.GetAllAsync(
            organization.Id,
            false,
            CancellationToken.None);

        var membership = Assert.Single(result);
        Assert.Equal(inactiveMembership.Id, membership.Id);
        Assert.False(membership.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMembershipBelongsToAnotherOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var otherOrganization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        dbContext.Organizations.Add(otherOrganization);
        await dbContext.SaveChangesAsync();

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.GetByIdAsync(
            otherOrganization.Id,
            membership.Id,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ChangeRoleAsync_UpdatesMembershipRole()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.ChangeRoleAsync(
            organization.Id,
            membership.Id,
            OrganizationRole.OrganizationAdmin,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(OrganizationRole.OrganizationAdmin, result.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenMembershipBelongsToAnotherOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var otherOrganization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        dbContext.Organizations.Add(otherOrganization);
        await dbContext.SaveChangesAsync();

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.ChangeRoleAsync(
            otherOrganization.Id,
            membership.Id,
            OrganizationRole.OrganizationAdmin,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(OrganizationRole.Viewer, membership.Role);
    }

    [Fact]
    public async Task SetActiveStatusAsync_DeactivatesAndReactivatesMembership()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.CaseWorker,
            Now);

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var deactivated = await service.SetActiveStatusAsync(
            organization.Id,
            membership.Id,
            false,
            CancellationToken.None);

        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);

        var reactivated = await service.SetActiveStatusAsync(
            organization.Id,
            membership.Id,
            true,
            CancellationToken.None);

        Assert.NotNull(reactivated);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_WhenMembershipBelongsToAnotherOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var otherOrganization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        dbContext.Organizations.Add(otherOrganization);
        await dbContext.SaveChangesAsync();

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.SetActiveStatusAsync(
            otherOrganization.Id,
            membership.Id,
            false,
            CancellationToken.None);

        Assert.Null(result);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WhenOrganizationIsInactive_ThrowsInvalidOperationException()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        organization.Deactivate(Now);

        await SeedAsync(dbContext, organization, user);

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                user.Id,
                organization.Id,
                OrganizationRole.Viewer,
                CancellationToken.None));

        Assert.Equal(
            "Cannot create a membership in an inactive organization.",
            exception.Message);

        Assert.Empty(dbContext.OrganizationMemberships);
    }

    [Fact]
    public async Task CreateAsync_WhenMembershipAlreadyExistsAndIsInactive_ThrowsInvalidOperationException()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        await SeedAsync(dbContext, organization, user);

        var existingMembership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        existingMembership.Deactivate(Now);

        dbContext.OrganizationMemberships.Add(existingMembership);
        await dbContext.SaveChangesAsync();

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(
                user.Id,
                organization.Id,
                OrganizationRole.CaseWorker,
                CancellationToken.None));

        var membership = Assert.Single(dbContext.OrganizationMemberships);
        Assert.Equal(existingMembership.Id, membership.Id);
        Assert.False(membership.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WhenOrganizationIsReactivated_CreatesActiveMembership()
    {
        await using var dbContext = CreateDbContext();

        var organization = CreateOrganization();
        var user = CreateUser();

        organization.Deactivate(Now);
        organization.Reactivate(Now.AddMinutes(1));

        await SeedAsync(dbContext, organization, user);

        var service = new OrganizationMembershipService(dbContext, new RecordingAuditLogger());

        var result = await service.CreateAsync(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            CancellationToken.None);

        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(user.Id, result.UserId);
        Assert.True(result.IsActive);
    }

}
