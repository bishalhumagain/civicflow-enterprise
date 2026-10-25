using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

public sealed class StaffApiIntegrationTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task CreateStaff_WithValidInput_ReturnsCreated()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
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
        Assert.True(json.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("", "valid@example.com")]
    [InlineData("Alex Morgan", "not-an-email")]
    public async Task CreateStaff_WithInvalidInput_ReturnsBadRequest(
        string fullName,
        string email)
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/staff",
            new { fullName, email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_WithDuplicateEmail_ReturnsConflict()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
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
    public async Task GetStaff_ReturnsStaffInTheSelectedOrganization()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();
        var email = $"officer-{Guid.NewGuid():N}@example.com";
        var seededStaff = await SeedStaffAsync(factory, "Jordan Lee", email);

        var response = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var staff = Assert.Single(
            json.RootElement.EnumerateArray(),
            entry => entry.GetProperty("email").GetString() == email);
        Assert.Equal(seededStaff.Id, staff.GetProperty("id").GetGuid());
        Assert.Equal("Jordan Lee", staff.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task GetStaff_FilteredByActiveStatus_ReturnsMatchingStaff()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();
        var staffMember = await SeedStaffAsync(
            factory,
            "Taylor Smith",
            $"officer-{Guid.NewGuid():N}@example.com");

        var deactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffMember.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var activeResponse = await client.GetAsync(
            "/api/staff?isActive=true");
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        using var activeJson = JsonDocument.Parse(
            await activeResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            activeJson.RootElement.EnumerateArray(),
            staff => staff.GetProperty("id").GetGuid() == staffMember.Id);

        var inactiveResponse = await client.GetAsync(
            "/api/staff?isActive=false");
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        using var inactiveJson = JsonDocument.Parse(
            await inactiveResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            inactiveJson.RootElement.EnumerateArray(),
            staff => staff.GetProperty("id").GetGuid() == staffMember.Id);
    }

    [Fact]
    public async Task DeactivateStaff_UpdatesStaffStatus()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();
        var staffMember = await SeedStaffAsync(
            factory,
            "Case Officer",
            $"case-{Guid.NewGuid():N}@example.com");

        var response = await client.PatchAsync(
            $"/api/staff/{staffMember.Id}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task ReactivateStaff_UpdatesStaffStatus()
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();
        var staffMember = await SeedStaffAsync(
            factory,
            "Review Officer",
            $"review-{Guid.NewGuid():N}@example.com");
        var deactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffMember.Id}/deactivate",
            content: null);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await client.PatchAsync(
            $"/api/staff/{staffMember.Id}/reactivate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task ChangeStaffStatus_WhenStaffDoesNotExist_ReturnsNotFound(
        string action)
    {
        using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/staff/{Guid.NewGuid()}/{action}",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<StaffMember> SeedStaffAsync(
        CivicFlowApiFactory factory,
        string fullName,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var staffMember = StaffMember.Create(
            OrganizationId,
            fullName,
            email,
            null,
            DateTimeOffset.UtcNow);

        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        return staffMember;
    }
}