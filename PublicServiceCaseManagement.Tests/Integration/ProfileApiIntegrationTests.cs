using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Profiles;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;
using PublicServiceCaseManagement.Api.Features.Profiles.Contracts;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class ProfileApiIntegrationTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

    private static async Task<ProfileResponse> ReadProfileAsync(
        HttpResponseMessage response)
    {
        var profile = await response.Content
            .ReadFromJsonAsync<ProfileResponse>(SerializerOptions);

        Assert.NotNull(profile);

        return profile!;
    }

    private static HttpClient CreateCitizenClient(CivicFlowApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);
        return client;
    }

    private static HttpClient CreateStaffClient(
        CivicFlowApiFactory factory,
        Guid organizationId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.StaffRole);
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());
        return client;
    }

    private static async Task SeedCitizenProfileAsync(
        CivicFlowApiFactory factory,
        string? nidNumber = null,
        string? citizenshipNumber = null)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var email = await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => user.Id == TestAuthenticationHandler.DefaultUserId)
            .Select(user => user.Email ?? string.Empty)
            .SingleAsync();

        var profile = CitizenProfile.Create(
            TestAuthenticationHandler.DefaultUserId,
            "CivicFlow Citizen Test User",
            email,
            "9800000000",
            nidNumber,
            citizenshipNumber,
            30,
            "Male",
            "Kathmandu",
            null,
            DateTimeOffset.UtcNow);

        dbContext.CitizenProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStaffMemberAsync(
        CivicFlowApiFactory factory,
        Guid organizationId,
        string? phoneNumber = null)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var email = await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => user.Id == TestAuthenticationHandler.DefaultUserId)
            .Select(user => user.Email ?? string.Empty)
            .SingleAsync();

        var staffMember = StaffMember.Create(
            organizationId,
            "CivicFlow Integration Test User",
            email,
            phoneNumber,
            DateTimeOffset.UtcNow);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMe_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/profile/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.SuperAdminRole);

        var response = await client.GetAsync("/api/profile/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_AsCitizen_ReturnsCitizenProfile()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        await SeedCitizenProfileAsync(factory);
        using var client = CreateCitizenClient(factory);

        var response = await client.GetAsync("/api/profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await ReadProfileAsync(response);

        Assert.Equal(ProfileAccountType.Citizen, profile.AccountType);
        Assert.Equal("9800000000", profile.PhoneNumber);
        Assert.Equal("Kathmandu", profile.PermanentAddress);
        Assert.Contains(TestAuthenticationHandler.CitizenRole, profile.Roles);
    }

    [Fact]
    public async Task PutMe_AsCitizen_UpdatesFullNameAndDetails()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        await SeedCitizenProfileAsync(factory);
        using var client = CreateCitizenClient(factory);

        var request = new UpdateProfileRequest(
            "  Bishal   Humagain  ",
            "9811111111",
            "123456789",
            "12-34-5678",
            41,
            "Male",
            "Pokhara",
            "Lalitpur");

        var response = await client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await ReadProfileAsync(response);

        Assert.Equal("Bishal Humagain", updated.FullName);
        Assert.Equal("9811111111", updated.PhoneNumber);
        Assert.Equal(41, updated.Age);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleAsync(item => item.Id == TestAuthenticationHandler.DefaultUserId);

        var citizenProfile = await dbContext.CitizenProfiles
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationUserId == user.Id);

        Assert.Equal("Bishal Humagain", user.FullName);
        Assert.Equal("Bishal Humagain", citizenProfile.FullName);
        Assert.Equal("9811111111", citizenProfile.PhoneNumber);
    }

    [Fact]
    public async Task PutMe_AsCitizen_WithoutExistingProfile_CreatesCitizenProfile()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var request = new UpdateProfileRequest(
            "Bishal Humagain",
            "9811111111",
            "987654321",
            null,
            35,
            "Male",
            "Kathmandu",
            null);

        var response = await client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await ReadProfileAsync(response);

        Assert.Equal("Bishal Humagain", updated.FullName);
        Assert.Equal("9811111111", updated.PhoneNumber);
        Assert.Equal("987654321", updated.NIDNumber);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var citizenProfile = await dbContext.CitizenProfiles
            .AsNoTracking()
            .SingleAsync(item =>
                item.ApplicationUserId == TestAuthenticationHandler.DefaultUserId);

        Assert.Equal("Bishal Humagain", citizenProfile.FullName);
        Assert.Equal("9811111111", citizenProfile.PhoneNumber);
    }

    [Fact]
    public async Task PutMe_AsCitizen_WithBlankFullName_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        await SeedCitizenProfileAsync(factory);
        using var client = CreateCitizenClient(factory);

        var request = new UpdateProfileRequest("   ");

        var response = await client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("Full name is required", body);
    }

    [Fact]
    public async Task PutMe_AsCitizen_WithInvalidPhoneNumber_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        await SeedCitizenProfileAsync(factory);
        using var client = CreateCitizenClient(factory);

        var request = new UpdateProfileRequest(
            "Bishal Humagain",
            "12");

        var response = await client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_AsCitizen_WithDuplicateNidNumber_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        await SeedCitizenProfileAsync(factory, nidNumber: "123456789");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var otherProfile = CitizenProfile.Create(
                Guid.NewGuid(),
                "Another Citizen",
                $"other-{Guid.NewGuid():N}@example.com",
                null,
                "123456789",
                null,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow);

            dbContext.CitizenProfiles.Add(otherProfile);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateCitizenClient(factory);

        var request = new UpdateProfileRequest(
            "Bishal Humagain",
            null,
            "123456789");

        var response = await client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_AsStaff_ReturnsStaffProfile()
    {
        await using var factory = new CivicFlowApiFactory();
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        await SeedStaffMemberAsync(factory, organizationId, "9800000000");
        using var client = CreateStaffClient(factory, organizationId);

        var response = await client.GetAsync("/api/profile/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await ReadProfileAsync(response);

        Assert.Equal(ProfileAccountType.Staff, profile.AccountType);
        Assert.Equal(organizationId.ToString(), profile.OrganizationId);
        Assert.Equal("Integration Test Organization", profile.OrganizationName);
        Assert.Equal(nameof(OrganizationRole.OrganizationAdmin), profile.OrganizationRole);
        Assert.Equal("9800000000", profile.PhoneNumber);
        Assert.Null(profile.NIDNumber);
    }

    [Fact]
    public async Task PutMe_AsStaff_UpdatesStaffRecord()
    {
        await using var factory = new CivicFlowApiFactory();
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        await SeedStaffMemberAsync(factory, organizationId);
        using var client = CreateStaffClient(factory, organizationId);

        var request = new UpdateProfileRequest(
            "Bishal Humagain",
            "9811111111");

        var response = await client.PutAsJsonAsync("/api/profile/me", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await ReadProfileAsync(response);

        Assert.Equal("Bishal Humagain", updated.FullName);
        Assert.Equal("9811111111", updated.PhoneNumber);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var staffMember = await dbContext.StaffMembers
            .AsNoTracking()
            .SingleAsync(item => item.OrganizationId == organizationId);

        Assert.Equal("Bishal Humagain", staffMember.FullName);
        Assert.Equal("9811111111", staffMember.PhoneNumber);
    }

    [Fact]
    public async Task GetMe_WhenBusinessEndpointsDisabled_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Production",
            enableBusinessEndpoints: false);
        await factory.SeedAuthenticatedPlatformUserAsync();
        await SeedCitizenProfileAsync(factory);
        using var client = CreateCitizenClient(factory);

        var response = await client.GetAsync("/api/profile/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
