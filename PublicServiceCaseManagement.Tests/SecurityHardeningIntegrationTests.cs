using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Platform;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Regression coverage for the security-hardening pass: development-time
/// dynamic Vite ports, immediate session revocation when the security stamp
/// changes (account deactivation / privilege change / password reset), and the
/// absolute session-lifetime decision.
/// </summary>
public sealed class SecurityHardeningIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";

    private static HttpClient CreateCookieClient(CivicFlowApiFactory factory)
    {
        var handler = factory.Server.CreateHandler();
        var cookieHandler = new CookieContainerHandler(handler);

        return new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private static async Task RegisterAndConfirmAsync(
        CivicFlowApiFactory factory,
        HttpClient client,
        string email)
    {
        var suffix = Math.Abs(email.GetHashCode()).ToString();

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Hardening Test User",
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

        var confirm = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { email, token });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task<Guid> ReadUserIdAsync(HttpClient client)
    {
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        return Guid.Parse(json.RootElement.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Development_AllowsAnyLoopbackVitePort_WithCredentials()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Development");

        using var client = factory.CreateClient();

        // A Vite port that is not in the static allowlist (5175+).
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/csrf");
        request.Headers.Add("Origin", "http://localhost:5175");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                "Access-Control-Allow-Origin",
                out var allowOriginValues));
        Assert.Equal(
            "http://localhost:5175",
            Assert.Single(allowOriginValues));

        Assert.True(
            response.Headers.TryGetValues(
                "Access-Control-Allow-Credentials",
                out var allowCredentialsValues));
        Assert.Equal(
            "true",
            Assert.Single(allowCredentialsValues));
    }

    [Fact]
    public async Task Development_DoesNotReflectPublicOrigins()
    {
        await using var factory = new CivicFlowApiFactory(
            environmentName: "Development");

        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/auth/csrf");
        request.Headers.Add("Origin", "https://evil-civicflow.example");

        var response = await client.SendAsync(request);

        // The loopback-only development rule must never reflect a public origin.
        Assert.False(response.Headers.TryGetValues(
            "Access-Control-Allow-Origin",
            out _));
    }

    [Fact]
    public async Task SecurityStampRotation_ImmediatelyRevokesCookie()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"stamp-revoke-{Guid.NewGuid():N}@example.com";
        await RegisterAndConfirmAsync(factory, client, email);
        await LoginAsync(client, email);

        // A live session works before the change.
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/auth/me")).StatusCode);

        // Simulate a password reset / privilege change / deactivation by
        // rotating the security stamp. The account stays active so the only
        // reason the next request can fail is the invalidated cookie.
        var userId = await ReadUserIdAsync(client);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            await userManager.UpdateSecurityStampAsync(user!);
        }

        // With per-request revalidation, the stale cookie is rejected at
        // authentication — proven by 401 rather than reaching the active-user
        // branch of /me.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task DeactivatingAccount_RevokesItsLiveSession()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"deactivate-revoke-{Guid.NewGuid():N}@example.com";
        await RegisterAndConfirmAsync(factory, client, email);
        await LoginAsync(client, email);

        var userId = await ReadUserIdAsync(client);

        using (var scope = factory.Services.CreateScope())
        {
            var directory =
                scope.ServiceProvider.GetRequiredService<IPlatformDirectoryService>();
            await directory.SetUserActiveStatusAsync(userId, isActive: false);
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public void AbsoluteSessionLifetime_RejectsAfterCapAndHonoursDisable()
    {
        var issued = DateTimeOffset.UtcNow;
        var cap = TimeSpan.FromHours(12);

        // Fresh session: not expired.
        Assert.False(
            SessionLifetimeMiddleware.IsExpired(issued, issued.AddMinutes(1), cap));

        // Beyond the hard cap: expired, regardless of sliding refresh.
        Assert.True(
            SessionLifetimeMiddleware.IsExpired(
                issued, issued.AddHours(12).AddSeconds(1), cap));

        // A non-positive cap disables the absolute check entirely.
        Assert.False(
            SessionLifetimeMiddleware.IsExpired(issued, issued.AddYears(5), TimeSpan.Zero));
    }

    [Fact]
    public async Task TokenCreation_IsRateLimited()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"token-rate-{Guid.NewGuid():N}@example.com";
        await RegisterAndConfirmAsync(factory, client, email);
        await LoginAsync(client, email);

        // Minting a personal access token is a sensitive action, so the create
        // endpoint is guarded by the fixed-window "auth" policy. A burst beyond
        // the per-partition permit limit must eventually be rejected with 429,
        // while a genuine earlier request still succeeds with 201.
        var sawCreated = false;
        var sawThrottled = false;

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/tokens",
                new { name = $"token-{attempt}", expiresInDays = 1 });

            if (response.StatusCode == HttpStatusCode.Created)
            {
                sawCreated = true;
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                sawThrottled = true;
                break;
            }
            else
            {
                Assert.Fail(
                    $"Unexpected status {(int)response.StatusCode} " +
                    $"{response.StatusCode} on attempt {attempt}.");
            }
        }

        Assert.True(
            sawCreated,
            "The first token creation must succeed before throttling kicks in.");
        Assert.True(
            sawThrottled,
            "A burst of token-creation requests must eventually be rate limited.");
    }
}
