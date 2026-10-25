using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class ErrorsApiIntegrationTests
{
    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=1000001")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=201")]
    public async Task GetErrors_RejectsOutOfRangePagination(string query)
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin,
            enableBusinessEndpoints: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "superadmin");

        var response = await client.GetAsync($"/api/errors{query}");

        Assert.Equal(
            System.Net.HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task MarkAsHandled_ReturnsNotFoundForUnknownError()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin,
            enableBusinessEndpoints: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "superadmin");

        var response = await client.PatchAsync(
            $"/api/errors/{Guid.NewGuid()}/mark-handled",
            content: null);

        Assert.Equal(
            System.Net.HttpStatusCode.NotFound,
            response.StatusCode);
    }
}