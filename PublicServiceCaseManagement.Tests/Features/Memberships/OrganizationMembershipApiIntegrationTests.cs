
using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Memberships;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Tests;

namespace PublicServiceCaseManagement.Tests.Features.Memberships;

public sealed class OrganizationMembershipApiIntegrationTests
{
    [Fact]
    public async Task CreateMembership_WithValidRequest_ReturnsCreatedMembership()
    {
        var (factory, client, organizationId, userId) =
            await CreateClientWithUserAsync();

        using (factory)
        using (client)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/organizations/{organizationId}/memberships",
                new
                {
                    UserId = userId,
                    Role = "CaseWorker"
                });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var membership =
                await response.Content.ReadFromJsonAsync<OrganizationMembershipResponse>();

            Assert.NotNull(membership);
            Assert.Equal(userId, membership.UserId);
            Assert.Equal(organizationId, membership.OrganizationId);
            Assert.Equal("CaseWorker", membership.Role);
            Assert.True(membership.IsActive);
        }
    }

    [Fact]
    public async Task CreateMembership_WithInvalidRole_ReturnsBadRequest()
    {
        var (factory, client, organizationId, userId) =
            await CreateClientWithUserAsync();

        using (factory)
        using (client)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/organizations/{organizationId}/memberships",
                new
                {
                    UserId = userId,
                    Role = "SuperAdmin"
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task MembershipEndpoints_CanListChangeRoleAndUpdateStatus()
    {
        var (factory, client, organizationId, userId) =
            await CreateClientWithUserAsync();

        using (factory)
        using (client)
        {
            var createResponse = await client.PostAsJsonAsync(
                $"/api/organizations/{organizationId}/memberships",
                new
                {
                    UserId = userId,
                    Role = "Viewer"
                });

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var createdMembership =
                await createResponse.Content.ReadFromJsonAsync<OrganizationMembershipResponse>();

            Assert.NotNull(createdMembership);

            var membershipId = createdMembership.Id;
            var baseUrl =
                $"/api/organizations/{organizationId}/memberships";

            var listResponse = await client.GetAsync(baseUrl);

            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var memberships =
                await listResponse.Content.ReadFromJsonAsync<
                    List<OrganizationMembershipResponse>>();

            Assert.NotNull(memberships);
            Assert.Contains(
                memberships,
                membership => membership.Id == membershipId);

            var changeRoleResponse = await client.PutAsJsonAsync(
                $"{baseUrl}/{membershipId}/role",
                new
                {
                    Role = "OrganizationAdmin"
                });

            Assert.Equal(HttpStatusCode.OK, changeRoleResponse.StatusCode);

            var updatedMembership =
                await changeRoleResponse.Content.ReadFromJsonAsync<
                    OrganizationMembershipResponse>();

            Assert.NotNull(updatedMembership);
            Assert.Equal("OrganizationAdmin", updatedMembership.Role);

            var deactivateResponse = await client.PatchAsync(
                $"{baseUrl}/{membershipId}/deactivate",
                null);

            Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

            var deactivatedMembership =
                await deactivateResponse.Content.ReadFromJsonAsync<
                    OrganizationMembershipResponse>();

            Assert.NotNull(deactivatedMembership);
            Assert.False(deactivatedMembership.IsActive);

            var reactivateResponse = await client.PatchAsync(
                $"{baseUrl}/{membershipId}/reactivate",
                null);

            Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);

            var reactivatedMembership =
                await reactivateResponse.Content.ReadFromJsonAsync<
                    OrganizationMembershipResponse>();

            Assert.NotNull(reactivatedMembership);
            Assert.True(reactivatedMembership.IsActive);
        }
    }

    [Fact]
    public async Task GetMembership_UsingDifferentOrganizationId_ReturnsNotFound()
    {
        var (factory, client, organizationId, userId) =
            await CreateClientWithUserAsync();

        using (factory)
        using (client)
        {
            var createResponse = await client.PostAsJsonAsync(
                $"/api/organizations/{organizationId}/memberships",
                new
                {
                    UserId = userId,
                    Role = "Viewer"
                });

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var membership =
                await createResponse.Content.ReadFromJsonAsync<
                    OrganizationMembershipResponse>();

            Assert.NotNull(membership);

            var differentOrganizationId = Guid.NewGuid();

            var response = await client.GetAsync(
                $"/api/organizations/{differentOrganizationId}/memberships/{membership.Id}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }


    private static async Task<(
        CivicFlowApiFactory Factory,
        HttpClient Client,
        Guid OrganizationId,
        Guid UserId)> CreateClientWithUserAsync()
    {
        var factory = new CivicFlowApiFactory();
        var client = factory.CreateClient();

        try
        {
            var organizationResponse = await client.PostAsJsonAsync(
                "/api/organizations",
                new
                {
                    name = "Membership Test Organization",
                    code = $"MEM-{Guid.NewGuid():N}"
                });

            organizationResponse.EnsureSuccessStatusCode();

            using var organizationJson = JsonDocument.Parse(
                await organizationResponse.Content.ReadAsStringAsync());

            var organizationId = organizationJson.RootElement
                .GetProperty("id")
                .GetGuid();

            using var scope = factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var user = ApplicationUser.Create(
                "Membership Integration Test User",
                $"membership-{Guid.NewGuid():N}@example.com",
                DateTimeOffset.UtcNow);

            dbContext.ApplicationUsers.Add(user);

            await dbContext.SaveChangesAsync();

            return (factory, client, organizationId, user.Id);
        }
        catch
        {
            client.Dispose();
            await factory.DisposeAsync();
            throw;
        }
    }

}
