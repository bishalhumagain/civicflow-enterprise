using System.Net;
using System.Net.Http.Json;
using PublicServiceCaseManagement.Api.Features.Citizens;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class CitizensApiIntegrationTests
{
    private static HttpClient CreateCitizenClient(CivicFlowApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);
        return client;
    }

    [Fact]
    public async Task GetMe_Anonymous_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/citizens/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithStaffClient_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/citizens/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithoutProfile_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var response = await client.GetAsync("/api/citizens/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithUnlinkedIdentity_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = CreateCitizenClient(factory);

        var response = await client.GetAsync("/api/citizens/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostMe_CreatesProfile_AndGetMeReturnsIt()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var request = new CreateCitizenProfileRequest(
            "Bishal Humagain",
            "bishal@example.com",
            "9800000000",
            "123456789",
            "12-34-5678",
            30,
            "Male",
            "Kathmandu",
            "Lalitpur");

        var createResponse = await client.PostAsJsonAsync(
            "/api/citizens/me",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<CitizenProfileResponse>();

        Assert.NotNull(created);
        Assert.Equal("Bishal Humagain", created!.FullName);
        Assert.Equal("bishal@example.com", created.Email);
        Assert.Equal("123456789", created.NIDNumber);

        var getResponse = await client.GetAsync("/api/citizens/me");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content
            .ReadFromJsonAsync<CitizenProfileResponse>();

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task PostMe_WhenProfileExists_UpdatesInPlace()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var first = new CreateCitizenProfileRequest(
            "Bishal Humagain",
            "bishal@example.com",
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var firstResponse = await client.PostAsJsonAsync(
            "/api/citizens/me",
            first);
        var created = await firstResponse.Content
            .ReadFromJsonAsync<CitizenProfileResponse>();

        var second = new CreateCitizenProfileRequest(
            "Bishal Humagain",
            "bishal@example.com",
            "9811111111",
            null,
            null,
            41,
            "Male",
            "Pokhara",
            null);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/citizens/me",
            second);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var updated = await secondResponse.Content
            .ReadFromJsonAsync<CitizenProfileResponse>();

        Assert.NotNull(updated);
        Assert.Equal(created!.Id, updated!.Id);
        Assert.Equal("9811111111", updated.PhoneNumber);
        Assert.Equal(41, updated.Age);
    }

    [Fact]
    public async Task PostMe_WithMissingRequiredFields_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var request = new CreateCitizenProfileRequest(
            string.Empty,
            "bishal@example.com",
            null, null, null, null, null, null, null);

        var response = await client.PostAsJsonAsync("/api/citizens/me", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_WithoutProfile_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var request = new UpdateCitizenProfileRequest(
            "9800000000", null, null, null, null, null, null);

        var response = await client.PutAsJsonAsync("/api/citizens/me", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostMe_WhenBusinessEndpointsDisabled_RemainsAvailable()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Production",
            enableBusinessEndpoints: false);
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateCitizenClient(factory);

        var request = new CreateCitizenProfileRequest(
            "Bishal Humagain",
            "bishal@example.com",
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var response = await client.PostAsJsonAsync("/api/citizens/me", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
