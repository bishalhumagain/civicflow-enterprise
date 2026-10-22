using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;
using PublicServiceCaseManagement.Tests.Infrastructure;

namespace PublicServiceCaseManagement.Tests.Features.Organizations;

public sealed class OrganizationDeleteTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private static (
        CivicFlowDbContext Context,
        OrganizationService Service) CreateService()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"OrganizationDelete_{Guid.NewGuid():N}")
            .Options;

        var context = new CivicFlowDbContext(options);

        return (
            context,
            new OrganizationService(context, new RecordingAuditLogger()));
    }

    [Fact]
    public async Task DeleteAsync_RemovesOrganizationAndItsAccessRecords()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organization = Organization.Create(
            "Organization One",
            "ORG-ONE",
            Now);

        context.Organizations.Add(organization);

        var userId = Guid.NewGuid();

        context.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                userId,
                organization.Id,
                OrganizationRole.CaseWorker,
                Now));

        context.StaffMembers.Add(
            StaffMember.Create(
                organization.Id,
                "Test Officer",
                "officer@example.com",
                null,
                Now));

        context.AccountInvitations.Add(
            AccountInvitation.Create(
                organization.Id,
                "invited@example.com",
                "Invited Officer",
                null,
                OrganizationRole.CaseWorker,
                new string('a', 64),
                userId,
                Now,
                Now.AddDays(3)));

        await context.SaveChangesAsync();

        var deleted = await service.DeleteAsync(
            organization.Id,
            CancellationToken.None);

        Assert.True(deleted);

        Assert.Empty(await context.Organizations.ToListAsync());
        Assert.Empty(await context.OrganizationMemberships.ToListAsync());
        Assert.Empty(await context.StaffMembers.ToListAsync());
        Assert.Empty(await context.AccountInvitations.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_RefusesWhenTheOrganizationStillHoldsCases()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organization = Organization.Create(
            "Organization One",
            "ORG-ONE",
            Now);

        context.Organizations.Add(organization);

        context.Cases.Add(
            Case.Create(
                organization.Id,
                "CF-0000000001",
                "Open case",
                "Description",
                Now));

        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(
                organization.Id,
                CancellationToken.None));

        Assert.Contains("case", exception.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Single(await context.Organizations.ToListAsync());
        Assert.Single(await context.Cases.ToListAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenOrganizationDoesNotExist()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var deleted = await service.DeleteAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(deleted);
    }
}
