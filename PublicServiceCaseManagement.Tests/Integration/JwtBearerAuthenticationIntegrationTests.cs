using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Tests.Support;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Integration;

/// <summary>
/// End-to-end coverage for the JWT Bearer scheme. These tests use the real
/// authentication pipeline (the test scheme is disabled) so token validation,
/// issuer/audience/signature/lifetime checks, role refresh, and organization
/// resolution are exercised exactly as in production.
/// </summary>
public sealed class JwtBearerAuthenticationIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";

    private static HttpClient CreateCookieClient(
        CivicFlowApiFactory factory)
    {
        var handler = factory.Server.CreateHandler();
        var cookieHandler = new CookieContainerHandler(handler);

        return new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private static async Task LoginAsCitizenAsync(
        CivicFlowApiFactory factory,
        HttpClient client,
        string email)
    {
        var suffix = Math.Abs(email.GetHashCode()).ToString();

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "JWT Cookie Test User",
                email,
                phoneNumber = "9800000000",
                nidNumber = $"NID-{suffix}",
                citizenshipNumber = $"CIT-{suffix}",
                password = StrongPassword
            });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var message = factory.EmailSender.LastFor(email);
        Assert.NotNull(message);

        var token = factory.EmailSender.ExtractToken(message!.Body, "token");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var verify = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { email, token });

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static HttpClient CreateBearerClient(
        CivicFlowApiFactory factory,
        string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task<(Guid UserId, Guid OrganizationId)> SeedStaffAsync(
        CivicFlowApiFactory factory)
    {
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();

        using var scope = factory.Services.CreateScope();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var user = await userManager.FindByIdAsync(
            TestAuthenticationHandler.DefaultUserId.ToString());

        Assert.NotNull(user);

        var role = await roleManager.FindByNameAsync(AppRoles.Staff);
        Assert.NotNull(role);

        // The seeded test user is inserted directly through the DbContext,
        // so link the Identity role directly rather than using
        // UserManager (which requires a populated security stamp).
        dbContext.Set<IdentityUserRole<Guid>>().Add(
            new IdentityUserRole<Guid>
            {
                UserId = user!.Id,
                RoleId = role!.Id
            });

        await dbContext.SaveChangesAsync();

        return (user.Id, organizationId);
    }

    private static string IssueValidToken(
        CivicFlowApiFactory factory,
        Guid userId)
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<IJwtAccessTokenIssuer>()
            .Issue(
                userId,
                "JWT Integration Test User",
                "jwt.integration@example.test",
                [AppRoles.Staff])
            .Token;
    }

    [Fact]
    public async Task ValidToken_AuthenticatesAndCarriesCurrentRolesAndOrganizationContext()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, organizationId) = await SeedStaffAsync(factory);
        var token = IssueValidToken(factory, userId);

        using var client = CreateBearerClient(factory, token);
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using var meJson = JsonDocument.Parse(
            await me.Content.ReadAsStringAsync());

        var roles = meJson.RootElement
            .GetProperty("roles")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(AppRoles.Staff, roles);

        // The organization context is still resolved server-side from the
        // database-backed membership, never from a claim in the token.
        var staff = await client.GetAsync("/api/staff");
        Assert.Equal(HttpStatusCode.OK, staff.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);

        var token = JwtTestTokenFactory.Create(
            userId,
            [AppRoles.Staff],
            notBefore: DateTimeOffset.UtcNow.AddHours(-2),
            expires: DateTimeOffset.UtcNow.AddMinutes(-5));

        using var client = CreateBearerClient(factory, token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);

        using var client = CreateBearerClient(
            factory,
            $"{userId}.not-a-valid.jwt");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuerToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);

        var token = JwtTestTokenFactory.Create(
            userId,
            [AppRoles.Staff],
            issuer: "https://wrong-issuer.example");

        using var client = CreateBearerClient(factory, token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudienceToken_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);

        var token = JwtTestTokenFactory.Create(
            userId,
            [AppRoles.Staff],
            audience: "https://wrong-audience.example");

        using var client = CreateBearerClient(factory, token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);

        var token = JwtTestTokenFactory.Create(
            userId,
            [AppRoles.Staff],
            signingKey: "wrong-signing-key-that-is-long-enough-0123456789");

        using var client = CreateBearerClient(factory, token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PersonalAccessToken_CanBeExchangedForShortLivedJwt()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);

        string personalAccessToken;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var (token, plaintext) = PersonalAccessToken.Create(
                userId,
                "jwt-exchange-test",
                DateTimeOffset.UtcNow,
                expiresAt: null);

            dbContext.PersonalAccessTokens.Add(token);
            await dbContext.SaveChangesAsync();

            personalAccessToken = plaintext;
        }

        using var tokenClient = CreateBearerClient(
            factory,
            personalAccessToken);

        var exchange = await tokenClient.PostAsync(
            AccessTokenRoutes.Base,
            content: null);

        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        using var exchangeJson = JsonDocument.Parse(
            await exchange.Content.ReadAsStringAsync());

        Assert.Equal(
            "Bearer",
            exchangeJson.RootElement.GetProperty("token_type").GetString());

        var accessToken = exchangeJson.RootElement
            .GetProperty("access_token")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        var expiresIn = exchangeJson.RootElement
            .GetProperty("expires_in")
            .GetInt32();

        Assert.InRange(expiresIn, 1, 60 * 60);

        // The long-lived PAT is not repeated in the response.
        Assert.DoesNotContain(
            personalAccessToken,
            await exchange.Content.ReadAsStringAsync());

        using var jwtClient = CreateBearerClient(factory, accessToken!);

        var me = await jwtClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task JwtBearerRequest_DoesNotRequireBrowserCsrfToken()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true)
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);
        var token = IssueValidToken(factory, userId);

        using var client = CreateBearerClient(factory, token);

        // Logout is an authenticated unsafe request. A cookie caller needs
        // the antiforgery token; a Bearer caller must not. The endpoint
        // returns 204 once authentication and authorization succeed.
        var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CookieSession_CannotMintJwtAndInvalidBearerDoesNotFallBackToCookie()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);
        await LoginAsCitizenAsync(
            factory,
            client,
            $"jwt-cookie-{Guid.NewGuid():N}@example.test");

        var exchange = await client.PostAsync(
            AccessTokenRoutes.Base,
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, exchange.StatusCode);

        // A bearer header must disable the ambient cookie rather than let a
        // malformed token fall through to a valid cookie session.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid.jwt");

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task TokenForInactiveAccount_IsRejected()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (userId, _) = await SeedStaffAsync(factory);
        var token = IssueValidToken(factory, userId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var user = await dbContext.ApplicationUsers
                .SingleAsync(candidate => candidate.Id == userId);

            user.Deactivate(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateBearerClient(factory, token);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_ExposesBearerAuthorizeScheme()
    {
        await using var factory = new CivicFlowApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var scheme = json.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task AnonymousCookieStylePost_StillRequiresCsrfToken()
    {
        await using var factory = new CivicFlowApiFactory(
            requireAntiforgery: true)
        {
            UseTestAuthentication = false
        };

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = "someone@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "antiforgery_invalid",
            json.RootElement.GetProperty("code").GetString());
    }
}
