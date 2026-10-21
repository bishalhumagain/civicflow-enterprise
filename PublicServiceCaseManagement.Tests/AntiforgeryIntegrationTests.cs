using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Verifies that the anti-forgery middleware rejects unsafe API requests
/// without a valid token and accepts them when the token is supplied.
/// </summary>
public sealed class AntiforgeryIntegrationTests
{
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

        // The middleware accepted the request; the endpoint itself returns 200.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
