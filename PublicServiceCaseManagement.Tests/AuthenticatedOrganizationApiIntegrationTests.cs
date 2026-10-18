using System.Net;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class AuthenticatedOrganizationApiIntegrationTests
{
    [Fact]
    public async Task GetCases_WithActiveMembership_ReturnsOk()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCases_WithoutOrganizationHeader_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCases_WithOrganizationWithoutMembership_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCases_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
