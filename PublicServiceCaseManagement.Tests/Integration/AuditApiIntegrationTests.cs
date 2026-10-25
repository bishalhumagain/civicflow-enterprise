using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class AuditApiIntegrationTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task OrganizationStaff_CanReadOnlyTheirOwnActivity()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker,
            enableBusinessEndpoints: true);
        using var client = factory.CreateClient();
        var currentUserId = TestAuthenticationHandler.DefaultUserId;
        var otherUserId = Guid.NewGuid();
        await SeedAuditEventsAsync(factory, currentUserId, otherUserId);

        var response = await client.GetAsync(
            $"/api/organizations/{OrganizationId}/audit?page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var events = json.RootElement.GetProperty("items")
            .EnumerateArray()
            .ToArray();

        var item = Assert.Single(events);
        Assert.Equal(
            currentUserId,
            item.GetProperty("actorUserId").GetGuid());

        var attemptedOverride = await client.GetAsync(
            $"/api/organizations/{OrganizationId}/audit" +
            $"?page=1&pageSize=25&actorUserId={otherUserId}");

        Assert.Equal(HttpStatusCode.OK, attemptedOverride.StatusCode);
        using var overriddenJson = JsonDocument.Parse(
            await attemptedOverride.Content.ReadAsStringAsync());
        Assert.Empty(
            overriddenJson.RootElement.GetProperty("items")
                .EnumerateArray());
        Assert.Equal(
            0,
            overriddenJson.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task OrganizationStaff_CannotReadAnotherOrganizationsActivity()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker,
            enableBusinessEndpoints: true);
        using var client = factory.CreateClient();
        var otherOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();

        var response = await client.GetAsync(
            $"/api/organizations/{otherOrganizationId}/audit?page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedAuditEventsAsync(
        CivicFlowApiFactory factory,
        Guid currentUserId,
        Guid otherUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var occurredAt = DateTimeOffset.UtcNow;

        dbContext.AuditEvents.AddRange(
            AuditEvent.Create(
                AuditCategory.Case,
                "case.updated",
                OrganizationId,
                currentUserId,
                "staff@example.test",
                "Case",
                Guid.NewGuid().ToString(),
                "Own activity",
                occurredAt),
            AuditEvent.Create(
                AuditCategory.Case,
                "case.updated",
                OrganizationId,
                otherUserId,
                "other@example.test",
                "Case",
                Guid.NewGuid().ToString(),
                "Another staff member's activity",
                occurredAt.AddSeconds(1)));
        await dbContext.SaveChangesAsync();
    }
}
