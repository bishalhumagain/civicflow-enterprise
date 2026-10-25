using System.Net;
using Xunit;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class AuthenticationIntegrationTests
{
    [Fact]
    public async Task GetStaff_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStaff_WithInvalidBearerToken_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", "invalid-test-token");

        var response = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
