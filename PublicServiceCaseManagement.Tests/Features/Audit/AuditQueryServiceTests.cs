using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Features.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Features.Audit;

public sealed class AuditQueryServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    private static (CivicFlowDbContext Context, AuditQueryService Service)
        CreateService()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"AuditQuery_{Guid.NewGuid():N}")
            .Options;

        var context = new CivicFlowDbContext(options);

        return (context, new AuditQueryService(context));
    }

    [Fact]
    public async Task GetPlatformEventsAsync_ReturnsEventsNewestFirst()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var organizationId = Guid.NewGuid();

        context.AuditEvents.AddRange(
            CreateEvent(
                AuditCategory.Organization,
                "organization.created",
                organizationId,
                Now),
            CreateEvent(
                AuditCategory.Case,
                "case.created",
                organizationId,
                Now.AddMinutes(5)));

        await context.SaveChangesAsync();

        var page = await service.GetPlatformEventsAsync(new AuditQuery());

        Assert.Equal(2, page.TotalCount);
        Assert.Equal("case.created", page.Items[0].Action);
        Assert.Equal("organization.created", page.Items[1].Action);
    }

    [Fact]
    public async Task GetOrganizationEventsAsync_OnlyReturnsOwnOrganization()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        var ownOrganizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();

        context.AuditEvents.AddRange(
            CreateEvent(
                AuditCategory.Organization,
                "organization.created",
                ownOrganizationId,
                Now),
            CreateEvent(
                AuditCategory.Organization,
                "organization.created",
                otherOrganizationId,
                Now));

        await context.SaveChangesAsync();

        var page = await service.GetOrganizationEventsAsync(
            ownOrganizationId,
            new AuditQuery());

        var item = Assert.Single(page.Items);

        Assert.Equal(ownOrganizationId, item.OrganizationId);
    }

    [Fact]
    public async Task GetPlatformEventsAsync_FiltersByCategoryAndAction()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        context.AuditEvents.AddRange(
            CreateEvent(AuditCategory.Case, "case.created", Guid.NewGuid(), Now),
            CreateEvent(AuditCategory.Case, "case.assigned", Guid.NewGuid(), Now),
            CreateEvent(AuditCategory.Organization, "organization.created", Guid.NewGuid(), Now));

        await context.SaveChangesAsync();

        var page = await service.GetPlatformEventsAsync(
            new AuditQuery { Category = "Case" });

        Assert.Equal(2, page.TotalCount);

        var actionPage = await service.GetPlatformEventsAsync(
            new AuditQuery { Action = "case.assigned" });

        Assert.Equal(1, actionPage.TotalCount);
        Assert.Equal("case.assigned", actionPage.Items[0].Action);
    }

    [Fact]
    public async Task GetPlatformEventsAsync_FiltersBySearchTerm()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        context.AuditEvents.AddRange(
            CreateEvent(
                AuditCategory.Organization,
                "organization.created",
                Guid.NewGuid(),
                Now,
                "Kathmandu Metropolitan City"),
            CreateEvent(
                AuditCategory.Organization,
                "organization.created",
                Guid.NewGuid(),
                Now,
                "Pokhara Metropolitan City"));

        await context.SaveChangesAsync();

        var page = await service.GetPlatformEventsAsync(
            new AuditQuery { Search = "pokhara" });

        var item = Assert.Single(page.Items);

        Assert.Contains("Pokhara", item.Details);
    }

    [Fact]
    public async Task GetPlatformEventsAsync_Paginates()
    {
        var (context, service) = CreateService();

        await using var _ = context;

        for (var index = 0; index < 5; index++)
        {
            context.AuditEvents.Add(
                CreateEvent(
                    AuditCategory.Case,
                    "case.created",
                    Guid.NewGuid(),
                    Now.AddMinutes(index)));
        }

        await context.SaveChangesAsync();

        var page = await service.GetPlatformEventsAsync(
            new AuditQuery { Page = 2, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.Page);
    }

    private static AuditEvent CreateEvent(
        AuditCategory category,
        string action,
        Guid organizationId,
        DateTimeOffset occurredAt,
        string details = "Details") =>
        AuditEvent.Create(
            category,
            action,
            organizationId,
            Guid.NewGuid(),
            "admin@example.com",
            "Organization",
            organizationId.ToString(),
            details,
            occurredAt);
}
