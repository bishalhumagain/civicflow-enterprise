
using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Memberships;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Tests;
using PublicServiceCaseManagement.Api.Features.Memberships.Contracts;

using PublicServiceCaseManagement.Tests.Support;

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
        var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
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

    [Fact]
    public async Task AssignCase_WhenUserIsViewer_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedAuthenticatedOrganizationAsync(
                role: OrganizationRole.Viewer);

        Guid caseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                organizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Viewer assignment test",
                "A viewer must not assign this case.",
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/assignment",
            new { staffMemberId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_WhenUserIsViewerAndCaseBelongsToOrganization_ReturnsOk()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedAuthenticatedOrganizationAsync(
                role: OrganizationRole.Viewer);

        Guid caseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                organizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Viewer read test",
                "A viewer can read this case.",
                DateTimeOffset.UtcNow);
            var staffMember = await dbContext.StaffMembers
                .SingleAsync(candidate =>
                    candidate.OrganizationId == organizationId);
            caseEntity.AssignTo(
                staffMember.Id,
                CaseAssignmentRole.Viewer,
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync($"/api/cases/{caseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStatusHistory_WhenCaseBelongsToAnotherSelectedOrganization_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var firstOrganizationId =
            await factory.SeedAuthenticatedOrganizationAsync();

        var secondOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();

        Guid caseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                firstOrganizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Status history isolation test",
                "This case belongs to another organization.",
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            secondOrganizationId.ToString());

        var response = await client.GetAsync(
            $"/api/cases/{caseId}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAssignmentHistory_WhenCaseBelongsToAnotherSelectedOrganization_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var firstOrganizationId =
            await factory.SeedAuthenticatedOrganizationAsync();

        var secondOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();

        Guid caseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                firstOrganizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Assignment history isolation test",
                "This case belongs to another organization.",
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            secondOrganizationId.ToString());

        var response = await client.GetAsync(
            $"/api/cases/{caseId}/assignment-history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_WhenAuthenticatedIdentityHasNoLinkedUser_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedOrganizationWithoutMembershipAsync();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync(
            $"/api/cases/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_WhenMembershipIsInactive_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedAuthenticatedOrganizationAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var membership = await dbContext.OrganizationMemberships
                .SingleAsync(m => m.OrganizationId == organizationId);

            membership.Deactivate(DateTimeOffset.UtcNow);

            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync(
            $"/api/cases/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_WhenOrganizationIsInactive_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedAuthenticatedOrganizationAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var organization = await dbContext.Organizations
                .SingleAsync(o => o.Id == organizationId);

            organization.Deactivate(DateTimeOffset.UtcNow);

            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync(
            $"/api/cases/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_WhenOrganizationHeaderIsInvalid_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            "not-a-guid");

        var response = await client.GetAsync(
            $"/api/cases/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

}
