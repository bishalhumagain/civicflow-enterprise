using System.Net;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class HealthApiIntegrationTests
{
    [Fact]
    public async Task ReadinessEndpoint_WithAvailableTestDatabase_ReturnsReady()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
