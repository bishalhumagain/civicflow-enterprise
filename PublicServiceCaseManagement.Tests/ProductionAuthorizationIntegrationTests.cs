using System.Net;

namespace PublicServiceCaseManagement.Tests;

public sealed class ProductionAuthorizationIntegrationTests
{
    [Fact]
    public async Task Production_AnonymousBusinessRequest_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Production");
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Production_CitizenClientBusinessRequest_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Production");
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Client-Id",
            TestAuthenticationHandler.CitizenClientId);

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Production_StaffClientBusinessRequest_RemainsDisabled()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Production");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Production_HealthEndpoint_AllowsAnonymousAccess()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Production");
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
