using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Auth;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

/// <summary>
/// End-to-end coverage of the account lifecycle using the real ASP.NET Core
/// Identity cookie authentication (the test authentication scheme is
/// disabled). Requests use a shared cookie container so the auth cookie
/// persists across calls.
/// </summary>
public sealed class AuthApiIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";
    private const string NewPassword = "Another-Strong-Passphrase-7#";

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

    private static async Task RegisterAsync(
        HttpClient client,
        string email)
    {
        var suffix = Math.Abs(email.GetHashCode()).ToString();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Lifecycle Test User",
                email,
                phoneNumber = "9800000000",
                nidNumber = $"NID-{suffix}",
                citizenshipNumber = $"CIT-{suffix}",
                password = StrongPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationCookie_ExpiresAfterThirtyMinutesOfInactivity()
    {
        await using var factory = new CivicFlowApiFactory();
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.True(options.SlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(30), options.ExpireTimeSpan);
    }

    private static async Task<string> ConfirmEmailAsync(
        CivicFlowApiFactory factory,
        HttpClient client,
        string email)
    {
        var message = factory.EmailSender.LastFor(email);

        Assert.NotNull(message);

        var token = factory.EmailSender.ExtractToken(
            message!.Body,
            "token");

        Assert.False(string.IsNullOrWhiteSpace(token));

        var response = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { email, token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return token!;
    }

    [Fact]
    public async Task Register_ThenLogin_RequiresEmailConfirmation()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"confirm-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutConfirmedEmail_ReturnsMachineReadableCode()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"confirmcode-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);

        var body = await loginResponse.Content.ReadFromJsonAsync<
            Dictionary<string, string>>();

        Assert.NotNull(body);
        Assert.Equal("email_not_confirmed", body!["code"]);
    }

    [Fact]
    public async Task ResendVerification_ResendsLinkForUnconfirmedAccount()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"resend-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);

        var resendResponse = await client.PostAsJsonAsync(
            "/api/auth/resend-verification",
            new { email });

        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);

        var message = factory.EmailSender.LastFor(email);
        Assert.NotNull(message);
        Assert.Contains("Verify your CivicFlow email", message.Subject);
        Assert.False(string.IsNullOrWhiteSpace(
            factory.EmailSender.ExtractToken(message.Body, "token")));
    }

    [Fact]
    public async Task Register_VerifyEmail_ThenLogin_Succeeds()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"login-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var savedUser = await dbContext.ApplicationUsers
                .SingleAsync(user => user.Email == email);

            Assert.False(string.IsNullOrWhiteSpace(savedUser.PasswordHash));
            Assert.DoesNotContain(StrongPassword, savedUser.PasswordHash);
            Assert.True(await userManager.CheckPasswordAsync(
                savedUser,
                StrongPassword));
        }

        await ConfirmEmailAsync(factory, client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await meResponse.Content
            .ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal(email, me!.Email);
        Assert.Contains("citizen", me.Roles);
    }

    [Fact]
    public async Task LegacyAccountWithoutPassword_CanResetAndSignIn()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);
        var email = $"legacy-password-{Guid.NewGuid():N}@example.com";
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = ApplicationUser.Create(
                "Migrated Citizen",
                email,
                now);
            user.EmailConfirmed = true;

            var createResult = await userManager.CreateAsync(user);
            Assert.True(createResult.Succeeded);

            var roleResult = await userManager.AddToRoleAsync(
                user,
                AppRoles.Citizen);
            Assert.True(roleResult.Succeeded);
        }

        var forgotResponse = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var resetMessage = factory.EmailSender.LastFor(email);
        Assert.NotNull(resetMessage);
        Assert.Contains("Reset your CivicFlow password", resetMessage!.Subject);

        var resetToken = factory.EmailSender.ExtractToken(
            resetMessage.Body,
            "token");
        Assert.False(string.IsNullOrWhiteSpace(resetToken));

        const string newPassword = "New-Legacy-Password-9!";
        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email, token = resetToken, newPassword });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var savedUser = await dbContext.ApplicationUsers
                .SingleAsync(user => user.Email == email);

            Assert.False(string.IsNullOrWhiteSpace(savedUser.PasswordHash));
            Assert.True(await userManager.CheckPasswordAsync(
                savedUser,
                newPassword));
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"wrongpass-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Wrong-Password-123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsGenericSuccess()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"dupe-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Duplicate User",
                email,
                phoneNumber = "9800000001",
                nidNumber = $"NID-DUP-{Guid.NewGuid():N}",
                citizenshipNumber = $"CIT-DUP-{Guid.NewGuid():N}",
                password = StrongPassword
            });

        // Enumeration-safe: the API still reports success.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Login_IsLockedOut_AfterRepeatedFailures()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"lockout-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        HttpStatusCode? lastStatus = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password = "Wrong-Password-123!" });

            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Locked, lastStatus);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsGenericSuccess()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email = $"missing-{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ThenReset_EnablesNewPasswordLogin()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"reset-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        var forgotResponse = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });

        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var message = factory.EmailSender.LastFor(email);
        Assert.NotNull(message);

        var token = factory.EmailSender.ExtractToken(
            message!.Body,
            "token");

        Assert.False(string.IsNullOrWhiteSpace(token));

        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new
            {
                email,
                token,
                newPassword = NewPassword
            });

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = NewPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_FindsAccountWhenLegacyNormalizedEmailIsStale()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"legacy-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var user = await dbContext.ApplicationUsers
                .SingleAsync(candidate => candidate.Email == email);

            user.EmailConfirmed = true;
            user.NormalizedEmail = "STALE_NORMALIZED_EMAIL";
            await dbContext.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RequiresAuthentication()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new
            {
                currentPassword = StrongPassword,
                newPassword = NewPassword
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ThenLoginWithNewPassword_Succeeds()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"changeself-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var changeResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new
            {
                currentPassword = StrongPassword,
                newPassword = NewPassword
            });

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var relogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = NewPassword });

        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSession()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"logout-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Register_CitizenCannotAccessStaffEndpoints()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"leastpriv-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        var staffResponse = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.Forbidden, staffResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Weak Password User",
                email = $"weak-{Guid.NewGuid():N}@example.com",
                phoneNumber = "9800000002",
                nidNumber = $"NID-WEAK-{Guid.NewGuid():N}",
                citizenshipNumber = $"CIT-WEAK-{Guid.NewGuid():N}",
                password = "password"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_AlwaysAssignsCitizenRole_NotPrivileged()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = $"rolecheck-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email);
        await ConfirmEmailAsync(factory, client, email);

        await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        var me = await (await client.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal("citizen", Assert.Single(me!.Roles));
        Assert.Equal("None", me.PlatformRole);
    }
}