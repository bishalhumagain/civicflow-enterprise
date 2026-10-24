using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Covers the directory administration and membership transfer surface:
/// citizens are listed from citizen profiles, profile edits are validated,
/// memberships can be moved between organizations, and invitation delivery
/// failures are surfaced without discarding the invitation.
/// </summary>
public sealed class DirectoryAndMembershipTransferIntegrationTests
{
    [Fact]
    public async Task GetCitizens_AsSuperAdmin_ReturnsCitizenProfiles()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        var citizen = await SeedCitizenAsync(factory);

        var response = await client.GetAsync("/api/platform/citizens");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var item = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(citizen.UserId, item.GetProperty("userId").GetGuid());
        Assert.Equal("Asha Citizen", item.GetProperty("fullName").GetString());
        Assert.Equal("NID-1001", item.GetProperty("nidNumber").GetString());
        Assert.True(item.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GetCitizens_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/citizens");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsSuperAdmin_FlagsCitizensAndStaff()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        var citizen = await SeedCitizenAsync(factory);

        var response = await client.GetAsync("/api/platform/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var users = json.RootElement.EnumerateArray().ToList();

        var staffUser = Assert.Single(
            users,
            item =>
                item.GetProperty("id").GetGuid() ==
                TestAuthenticationHandler.DefaultUserId);
        Assert.True(staffUser.GetProperty("isStaff").GetBoolean());
        Assert.False(staffUser.GetProperty("isCitizen").GetBoolean());
        Assert.NotEmpty(staffUser.GetProperty("organizations").EnumerateArray());

        var citizenUser = Assert.Single(
            users,
            item => item.GetProperty("id").GetGuid() == citizen.UserId);
        Assert.True(citizenUser.GetProperty("isCitizen").GetBoolean());
        Assert.False(citizenUser.GetProperty("isStaff").GetBoolean());
        Assert.Empty(citizenUser.GetProperty("organizations").EnumerateArray());
    }

    [Fact]
    public async Task UpdateCitizen_PersistsProfileAndAccountName()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        var citizen = await SeedCitizenAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/platform/citizens/{citizen.UserId}",
            new
            {
                fullName = "Asha Updated",
                phoneNumber = "9800000000",
                nidNumber = "NID-2002",
                citizenshipNumber = "CIT-2002",
                age = 31,
                gender = "Female",
                permanentAddress = "Kathmandu",
                temporaryAddress = "Lalitpur"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal("Asha Updated", json.RootElement.GetProperty("fullName").GetString());
        Assert.Equal("NID-2002", json.RootElement.GetProperty("nidNumber").GetString());
        Assert.Equal(31, json.RootElement.GetProperty("age").GetInt32());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        var user = await dbContext.ApplicationUsers.SingleAsync(item => item.Id == citizen.UserId);
        var profile = await dbContext.CitizenProfiles.SingleAsync(
            item => item.ApplicationUserId == citizen.UserId);
        Assert.Equal("Asha Updated", user.FullName);
        Assert.Equal("Kathmandu", profile.PermanentAddress);
    }

    [Fact]
    public async Task UpdateCitizen_WithDuplicateNid_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        var first = await SeedCitizenAsync(factory);
        _ = await SeedCitizenAsync(factory, "NID-1001");

        var response = await client.PutAsJsonAsync(
            $"/api/platform/citizens/{first.UserId}",
            new
            {
                fullName = "Asha Citizen",
                nidNumber = "NID-1001"
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TransferMembership_MovesMemberAndDeactivatesSource()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        var (sourceOrganizationId, membershipId, userId) =
            await SeedSecondMemberAsync(factory);
        var destinationId = await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{sourceOrganizationId}/memberships/{membershipId}/transfer",
            new
            {
                destinationOrganizationId = destinationId,
                role = "CaseWorker"
            });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"status={response.StatusCode} body={body}");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        var sourceMembership = await dbContext.OrganizationMemberships
            .SingleAsync(item => item.Id == membershipId);
        Assert.False(sourceMembership.IsActive);

        var destinationMembership = await dbContext.OrganizationMemberships
            .SingleAsync(item =>
                item.UserId == userId &&
                item.OrganizationId == destinationId);
        Assert.True(destinationMembership.IsActive);
        Assert.Equal(OrganizationRole.CaseWorker, destinationMembership.Role);
    }

    [Fact]
    public async Task TransferMembership_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();
        var (sourceOrganizationId, membershipId, _) =
            await SeedSecondMemberAsync(factory);
        var destinationId = await factory.SeedOrganizationWithoutMembershipAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{sourceOrganizationId}/memberships/{membershipId}/transfer",
            new
            {
                destinationOrganizationId = destinationId,
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitation_WhenDeliveryFails_KeepsInvitationAndReportsFailure()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        factory.EmailSender.FailDelivery = true;

        var organizationId = await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();
        var email = $"undeliverable-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new
            {
                fullName = "Undeliverable Invitee",
                email,
                role = "CaseWorker"
            });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"status={response.StatusCode} body={body}");

        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("deliveryFailed").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        var invitation = await dbContext.AccountInvitations.SingleAsync(
            item => item.OrganizationId == organizationId);
        Assert.True(invitation.IsDeliveryFailed);
        Assert.False(string.IsNullOrWhiteSpace(invitation.DeliveryError));
        Assert.Null(invitation.UsedAt);
    }

    [Fact]
    public async Task ResendInvitation_AfterDeliveryFailure_ClearsFailureState()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();
        factory.EmailSender.FailDelivery = true;

        var organizationId = await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();
        var email = $"retry-{Guid.NewGuid():N}@example.com";

        var createResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new { fullName = "Retry Invitee", email, role = "Viewer" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var invitationId = createdJson.RootElement.GetProperty("id").GetGuid();

        factory.EmailSender.FailDelivery = false;
        var resendResponse = await client.PostAsync(
            $"/api/organizations/{organizationId}/invitations/{invitationId}/resend",
            null);

        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        using var resentJson = JsonDocument.Parse(
            await resendResponse.Content.ReadAsStringAsync());
        Assert.False(resentJson.RootElement.GetProperty("deliveryFailed").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        var invitation = await dbContext.AccountInvitations
            .SingleAsync(item => item.Id == invitationId);
        Assert.False(invitation.IsDeliveryFailed);
    }

    private static async Task<(Guid UserId, string Email)> SeedCitizenAsync(
        CivicFlowApiFactory factory,
        string nidNumber = "NID-1001")
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"citizen-{Guid.NewGuid():N}@example.com";
        var user = ApplicationUser.Create("Asha Citizen", email, now);
        typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id))!.SetValue(user, userId);
        var profile = CitizenProfile.Create(
            userId,
            "Asha Citizen",
            email,
            "9811111111",
            nidNumber,
            null,
            29,
            "Female",
            "Kathmandu",
            null,
            now);

        dbContext.ApplicationUsers.Add(user);
        dbContext.CitizenProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        return (userId, email);
    }

    private static async Task<(Guid SourceOrganizationId, Guid MembershipId, Guid UserId)>
        SeedSecondMemberAsync(CivicFlowApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var organization = Organization.Create(
            "Transfer Source Organization",
            $"SRC-{Guid.NewGuid():N}",
            now);
        var userId = Guid.NewGuid();
        var user = ApplicationUser.Create(
            "Transfer Member",
            $"transfer-{Guid.NewGuid():N}@example.com",
            now);
        typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id))!.SetValue(user, userId);
        var membership = OrganizationMembership.Create(
            userId,
            organization.Id,
            OrganizationRole.CaseWorker,
            now);
        var administratorMembership = OrganizationMembership.Create(
            TestAuthenticationHandler.DefaultUserId,
            organization.Id,
            OrganizationRole.OrganizationAdmin,
            now);

        dbContext.Organizations.Add(organization);
        dbContext.ApplicationUsers.Add(user);
        dbContext.OrganizationMemberships.AddRange(
            membership,
            administratorMembership);
        await dbContext.SaveChangesAsync();

        return (organization.Id, membership.Id, userId);
    }
}
