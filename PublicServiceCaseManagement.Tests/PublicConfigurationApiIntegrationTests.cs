using System.Net;
using System.Net.Http.Json;

namespace PublicServiceCaseManagement.Tests;

public sealed class PublicConfigurationApiIntegrationTests
{
    [Fact]
    public async Task PublicConfiguration_IsAnonymousAndReturnsConfiguredApplicationName()
    {
        await using var factory = new CivicFlowApiFactory(
            settings: new Dictionary<string, string?>
            {
                ["Application:Name"] = "CivicFlow Test Suite"
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/config/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PublicConfigurationResponse>();
        Assert.NotNull(body);
        Assert.Equal("CivicFlow Test Suite", body.ApplicationName);
    }

    [Fact]
    public async Task PublicConfiguration_FallsBackWhenApplicationNameIsBlank()
    {
        await using var factory = new CivicFlowApiFactory(
            settings: new Dictionary<string, string?>
            {
                ["Application:Name"] = " "
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/config/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PublicConfigurationResponse>();
        Assert.NotNull(body);
        Assert.Equal("CivicFlow Enterprise", body.ApplicationName);
    }

    private sealed record PublicConfigurationResponse(string ApplicationName);
}
