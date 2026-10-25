using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Features.Profiles;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Cross-cutting API checks for the shared normalization and validation
/// rules. These tests exercise the request boundary rather than domain
/// methods so overposting, padded input and malformed payloads are covered.
/// </summary>
public sealed class InputValidationIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";

    private static HttpClient CreateClient(
        CivicFlowApiFactory factory,
        string? role = null)
    {
        var client = factory.CreateClient();

        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
        }

        return client;
    }

    private static object CreateRegistrationPayload(
        string fullName,
        string email,
        string phoneNumber,
        string password = StrongPassword)
    {
        var suffix = Guid.NewGuid().ToString("N");

        return new
        {
            fullName,
            email,
            phoneNumber,
            nidNumber = "NID-" + suffix,
            citizenshipNumber = "CIT-" + suffix,
            password
        };
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task Register_WithWhitespaceOnlyFullName_ReturnsBadRequest(
        string fullName)
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };
        using var client = factory.CreateClient();
        var email = "register-name-" + Guid.NewGuid().ToString("N") +
            "@example.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            CreateRegistrationPayload(fullName, email, "9800000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithFullNameOverMaximumLength_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };
        using var client = factory.CreateClient();
        var email = "register-long-name-" + Guid.NewGuid().ToString("N") +
            "@example.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            CreateRegistrationPayload(
                new string((char)65, 151),
                email,
                "9800000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("two words@example.test")]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest(string email)
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            CreateRegistrationPayload(
                "Validation Test User",
                email,
                "9800000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("98000O0000")]
    [InlineData("+977-980000000000000000000000000000000")]
    public async Task Register_WithInvalidPhoneNumber_ReturnsBadRequest(
        string phoneNumber)
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };
        using var client = factory.CreateClient();
        var email = "register-phone-" + Guid.NewGuid().ToString("N") +
            "@example.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            CreateRegistrationPayload(
                "Validation Test User",
                email,
                phoneNumber));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_NormalizesUserSuppliedValues()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = "Mixed-Case-" + suffix + "@Example.TEST";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "  Bishal   Humagain  ",
                email = "  " + email + "  ",
                phoneNumber = "  9800000000  ",
                nidNumber = "  nid-" + suffix + "  ",
                citizenshipNumber = "  cit-" + suffix + "  ",
                password = StrongPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var normalizedEmail = email.ToLowerInvariant();
        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Email == normalizedEmail);

        Assert.Equal("Bishal Humagain", user.FullName);

        var profile = await dbContext.CitizenProfiles
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.ApplicationUserId == user.Id);

        Assert.Equal("Bishal Humagain", profile.FullName);
        Assert.Equal(normalizedEmail, profile.Email);
        Assert.Equal("9800000000", profile.PhoneNumber);
        Assert.Equal(
            ("NID-" + suffix).ToUpperInvariant(),
            profile.NIDNumber);
        Assert.Equal(
            ("CIT-" + suffix).ToUpperInvariant(),
            profile.CitizenshipNumber);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task UpdateProfile_WithWhitespaceOnlyName_ReturnsBadRequest(
        string fullName)
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateClient(factory, TestAuthenticationHandler.CitizenRole);

        var response = await client.PutAsJsonAsync(
            "/api/profile/me",
            new UpdateProfileRequest(
                fullName,
                "9800000000",
                null,
                null,
                30,
                null,
                null,
                null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(151)]
    public async Task UpdateProfile_WithAgeOutOfRange_ReturnsBadRequest(int age)
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateClient(factory, TestAuthenticationHandler.CitizenRole);

        var response = await client.PutAsJsonAsync(
            "/api/profile/me",
            new UpdateProfileRequest(
                "Validation Test User",
                "9800000000",
                null,
                null,
                age,
                null,
                null,
                null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("98000O0000")]
    public async Task UpdateProfile_WithInvalidPhone_ReturnsBadRequest(
        string phoneNumber)
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateClient(factory, TestAuthenticationHandler.CitizenRole);

        var response = await client.PutAsJsonAsync(
            "/api/profile/me",
            new UpdateProfileRequest(
                "Validation Test User",
                phoneNumber,
                null,
                null,
                30,
                null,
                null,
                null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task CreateCase_WithWhitespaceOnlyTitle_ReturnsBadRequest(
        string title)
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title,
                description = "A sufficiently long description for testing."
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCase_NormalizesTitleAndDescription()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "  Integration   test  case  ",
                description =
                    "  Description line one.\n\nDescription line two.  "
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Integration test case",
            json.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "Description line one.\n\nDescription line two.",
            json.RootElement.GetProperty("description").GetString());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Notifications_WithOutOfRangePagination_ReturnsBadRequest(
        string query)
    {
        await using var factory = new CivicFlowApiFactory();
        await factory.SeedAuthenticatedPlatformUserAsync();
        using var client = CreateClient(factory, TestAuthenticationHandler.CitizenRole);

        var response = await client.GetAsync("/api/notifications?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadDocument_WithMismatchedPdfSignature_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Document signature validation case",
                description = "Used to verify declared media types."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var caseJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = caseJson.RootElement.GetProperty("id").GetGuid();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(
            Encoding.UTF8.GetBytes("this is not a pdf document"));
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "fake.pdf");

        var response = await client.PostAsync(
            "/api/cases/" + caseId + "/documents",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            "does not match the declared type application/pdf",
            body);
    }

    [Fact]
    public async Task UploadDocument_WithEmptyFile_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Empty document validation case",
                description = "Used to verify empty uploads are rejected."
            });

        using var caseJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = caseJson.RootElement.GetProperty("id").GetGuid();

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "empty.bin");

        var response = await client.PostAsync(
            "/api/cases/" + caseId + "/documents",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("The file cannot be empty.", body);
    }
}
