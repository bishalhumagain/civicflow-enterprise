using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

/// <summary>
/// Verifies that the anti-forgery middleware rejects unsafe API requests
/// without a valid token and accepts them when the token is supplied.
/// </summary>
public sealed class AntiforgeryIntegrationTests
{
    [Fact]
    public async Task SwaggerUi_UsesAntiforgeryInterceptorForCookieRequests()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/index.html");
        var html = await response.Content.ReadAsStringAsync();
        using var initializerResponse =
            await client.GetAsync("/swagger/index.js");
        var initializer =
            await initializerResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, initializerResponse.StatusCode);
        Assert.Contains("/swagger/v1/swagger.json", initializer);
        Assert.Contains("requestInterceptor", initializer);
        Assert.Contains("X-CSRF-TOKEN", initializer);
        Assert.Contains("/api/auth/csrf", initializer);
        Assert.Contains(
            "'unsafe-inline'",
            response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Post_WithoutToken_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = "someone@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithToken_PassesAntiforgeryValidation()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true);

        var handler = factory.Server.CreateHandler();
        using var cookieHandler = new CookieContainerHandler(handler);
        using var client = new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);

        var csrfJson = JsonDocument.Parse(
            await csrfResponse.Content.ReadAsStringAsync());

        var token = csrfJson.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(
                new { email = "someone@example.com" })
        };

        request.Headers.Add("X-CSRF-TOKEN", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutToken_ReturnsStableErrorCode()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = "someone@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "antiforgery_invalid",
            json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AntiforgeryFailure_RefreshTokenRetry_Succeeds()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true);

        var handler = factory.Server.CreateHandler();
        using var cookieHandler = new CookieContainerHandler(handler);
        using var client = new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);

        using var staleRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(
                new { email = "someone@example.com" })
        };

        staleRequest.Headers.Add("X-CSRF-TOKEN", "stale-token-from-previous-user");

        var staleResponse = await client.SendAsync(staleRequest);

        Assert.Equal(HttpStatusCode.BadRequest, staleResponse.StatusCode);

        using var errorJson = JsonDocument.Parse(
            await staleResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "antiforgery_invalid",
            errorJson.RootElement.GetProperty("code").GetString());

        var refreshedResponse = await client.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);

        using var refreshedJson = JsonDocument.Parse(
            await refreshedResponse.Content.ReadAsStringAsync());

        var freshToken = refreshedJson.RootElement
            .GetProperty("token")
            .GetString();

        using var retryRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/forgot-password")
        {
            Content = JsonContent.Create(
                new { email = "someone@example.com" })
        };

        retryRequest.Headers.Add("X-CSRF-TOKEN", freshToken);

        var retryResponse = await client.SendAsync(retryRequest);

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
    }
}
