using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Auth;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// End-to-end coverage of the personal access token subsystem: creation,
/// bearer authentication, role propagation, revocation, and expiry.
/// </summary>
public sealed class PersonalAccessTokenApiIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";

    private static HttpClient CreateCookieClient(
        WebApplicationFactory<Program> factory)
    {
        var handler = factory.Server.CreateHandler();
        var cookieHandler = new CookieContainerHandler(handler);

        return new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private static async Task RegisterAndSignInAsync(
        CivicFlowApiFactory factory,
        HttpClient client,
        string email)
    {
        var suffix = Guid.NewGuid().ToString("N");

        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Access Token User",
                email,
                phoneNumber = "9800000003",
                nidNumber = $"NID-PAT-{suffix}",
                citizenshipNumber = $"CIT-PAT-{suffix}",
                password = StrongPassword
            });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var message = factory.EmailSender.LastFor(email);
        Assert.NotNull(message);

        var token = factory.EmailSender.ExtractToken(message!.Body, "token");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { email, token });

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task CreateToken_RequiresAuthentication()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Anonymous token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateToken_ReturnsPlaintextOnce_AndStoresOnlyHash()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-create-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Reporting script", expiresInDays = 90 });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        Assert.NotNull(created);
        Assert.StartsWith("civicflow_pat_", created!.Token);
        Assert.Equal("Reporting script", created.Name);
        Assert.NotNull(created.ExpiresAt);

        // The plaintext must never be persisted; only a hash is stored.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var stored = await dbContext.PersonalAccessTokens
            .AsNoTracking()
            .SingleAsync();

        Assert.NotEqual(created.Token, stored.TokenHash);
        Assert.DoesNotContain(created.Token, stored.TokenHash);
        Assert.Equal(
            PersonalAccessToken.Hash(created.Token),
            stored.TokenHash);

        // Listing never returns the plaintext again.
        var listResponse = await client.GetAsync("/api/auth/tokens");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listed = await listResponse.Content
            .ReadFromJsonAsync<List<PersonalAccessTokenResponse>>();

        Assert.NotNull(listed);
        var single = Assert.Single(listed!);
        Assert.Equal("Reporting script", single.Name);
        Assert.True(single.IsActive);
    }

    [Fact]
    public async Task Token_AuthenticatesAsBearer_AndCarriesTheUserRoles()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-bearer-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "CLI" });

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        using var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", created!.Token);

        var meResponse = await tokenClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await meResponse.Content
            .ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal(email, me!.Email);
        Assert.Contains("citizen", me.Roles);
    }

    [Fact]
    public async Task Token_CannotAccessStaffEndpoints_LikeTheCookieSession()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-leastpriv-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Citizen token" });

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        using var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", created!.Token);

        var staffResponse = await tokenClient.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.Forbidden, staffResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeToken_InvalidatesTheBearerCredential()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-revoke-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Revocable token" });

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        using var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", created!.Token);

        Assert.Equal(
            HttpStatusCode.OK,
            (await tokenClient.GetAsync("/api/auth/me")).StatusCode);

        var revokeResponse = await client.DeleteAsync(
            $"/api/auth/tokens/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await tokenClient.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RevokeToken_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-owner-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Owned token" });

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        var revokeResponse = await client.DeleteAsync(
            $"/api/auth/tokens/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, revokeResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/auth/tokens");
        var listed = await listResponse.Content
            .ReadFromJsonAsync<List<PersonalAccessTokenResponse>>();

        Assert.NotNull(listed);
        Assert.Equal(created!.Id, Assert.Single(listed!).Id);
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                "civicflow_pat_000000_deadbeef");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-tamper-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Tamper target" });

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        var tampered = created!.Token[..^4] + "AAAA";

        using var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tampered);

        var response = await tokenClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-expired-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var createResponse = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "Short lived", expiresInDays = 1 });

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreatePersonalAccessTokenResponse>();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var stored = await dbContext.PersonalAccessTokens.SingleAsync();

            typeof(PersonalAccessToken)
                .GetProperty(nameof(PersonalAccessToken.ExpiresAt))!
                .SetValue(
                    stored,
                    DateTimeOffset.UtcNow.AddDays(-1));

            await dbContext.SaveChangesAsync();
        }

        using var tokenClient = factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", created!.Token);

        var response = await tokenClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateToken_RejectsBlankName()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"pat-blank-{Guid.NewGuid():N}@example.com";
        await RegisterAndSignInAsync(factory, client, email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/tokens",
            new { name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
