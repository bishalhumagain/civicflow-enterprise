
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class StaffApiIntegrationTests
{
    [Fact]
    public async Task CreateStaff_WithValidInput_ReturnsCreated()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Alex Morgan",
                email = $"alex-{Guid.NewGuid():N}@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Alex Morgan",
            json.RootElement.GetProperty("fullName").GetString());

        Assert.True(
            json.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GetStaff_ReturnsCreatedStaffMember()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var email = $"officer-{Guid.NewGuid():N}@example.com";

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Jordan Lee",
                email
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var staffMember = Assert.Single(
            json.RootElement.EnumerateArray(),
            staff => staff.GetProperty("email").GetString() == email);        Assert.Equal(
            "Jordan Lee",
            staffMember.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task GetStaff_FilteredByActiveStatus_ReturnsMatchingStaff()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var email = $"officer-{Guid.NewGuid():N}@example.com";

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Taylor Smith",
                email
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var staffId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var deactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffId}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var activeResponse = await client.GetAsync(
            "/api/staff?isActive=true");

        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);

        using var activeJson = JsonDocument.Parse(
            await activeResponse.Content.ReadAsStringAsync());

        Assert.DoesNotContain(
            activeJson.RootElement.EnumerateArray(),
            staff => staff.GetProperty("id").GetGuid() == staffId);

        var inactiveResponse = await client.GetAsync(
            "/api/staff?isActive=false");

        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);

        using var inactiveJson = JsonDocument.Parse(
            await inactiveResponse.Content.ReadAsStringAsync());

        Assert.Contains(
            inactiveJson.RootElement.EnumerateArray(),
            staff => staff.GetProperty("id").GetGuid() == staffId);
    }

    [Fact]
    public async Task DeactivateStaff_UpdatesStaffStatus()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Case Officer",
                email = $"case-{Guid.NewGuid():N}@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var staffId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var response = await client.PatchAsync(
            $"/api/staff/{staffId}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var updatedJson = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.False(
            updatedJson.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task ReactivateStaff_UpdatesStaffStatus()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Review Officer",
                email = $"review-{Guid.NewGuid():N}@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var staffId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var deactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffId}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var reactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffId}/reactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);

        using var updatedJson = JsonDocument.Parse(
            await reactivateResponse.Content.ReadAsStringAsync());

        Assert.True(
            updatedJson.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("", "valid@example.com")]
    [InlineData("Alex Morgan", "not-an-email")]
    public async Task CreateStaff_WithInvalidInput_ReturnsBadRequest(
        string fullName,
        string email)
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/staff",
            new { fullName, email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_WithDuplicateEmail_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var email = $"duplicate-{Guid.NewGuid():N}@example.com";

        var firstResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Alex Morgan",
                email
            });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Jordan Lee",
                email
            });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivateStaff_WhenStaffDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/staff/{Guid.NewGuid()}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReactivateStaff_WhenStaffDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/staff/{Guid.NewGuid()}/reactivate",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

}
