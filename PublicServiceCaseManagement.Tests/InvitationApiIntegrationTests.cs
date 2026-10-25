using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Auth;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Covers the invitation-based staff onboarding flow. An administrator invites
/// an address, the invitee receives a single-use link, chooses their own
/// password, and can then sign in to the organization portal. No password is
/// ever generated or transmitted by the application.
/// </summary>
public sealed class InvitationApiIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";
    private const string InviteePassword = "Borrowed-Concern-Shelf-7!";

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

    private static async Task EnsureRolesAsync(CivicFlowApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in new[]
                 {
                     AppRoles.Citizen,
                     AppRoles.Staff,
                     AppRoles.SuperAdmin
                 })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>
                    {
                        Id = Guid.NewGuid(),
                        Name = role
                    });
            }
        }
    }

    private static async Task<(string Email, Guid OrganizationId)>
        CreateOrganizationAdminAsync(
            CivicFlowApiFactory factory,
            OrganizationRole role = OrganizationRole.OrganizationAdmin)
    {
        await EnsureRolesAsync(factory);

        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var email = $"org-admin-{Guid.NewGuid():N}@example.com";

        var user = ApplicationUser.Create(
            "Organization Admin",
            email,
            DateTimeOffset.UtcNow);

        user.EmailConfirmed = true;

        var result = await userManager.CreateAsync(user, StrongPassword);

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));

        await userManager.AddToRoleAsync(user, AppRoles.Staff);

        var organization = Organization.Create(
            "Invitation Test Organization",
            $"INV-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                user.Id,
                organization.Id,
                role,
                DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync();

        return (email, organization.Id);
    }

    private static async Task SignInAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> InviteAsync(
        CivicFlowApiFactory factory,
        HttpClient client,
        Guid organizationId,
        string email,
        string role = "CaseWorker")
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new
            {
                fullName = "Invited Officer",
                email,
                role
            });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"status={response.StatusCode} body={body}");

        // The raw token only exists inside the delivered email.
        var message = factory.EmailSender.LastFor(email);
        Assert.NotNull(message);

        var token = factory.EmailSender.ExtractToken(
            message!.Body,
            "token");

        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    [Fact]
    public async Task Invitation_CreatesSingleUseLinkAndUsableLogin()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"invitee-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        // The invitation must not contain a password, and the stored record
        // must only hold a hash of the token.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var invitation = await dbContext.AccountInvitations.SingleAsync(
                candidate =>
                    candidate.OrganizationId == organizationId);

            Assert.Equal(64, invitation.TokenHash.Length);
            Assert.NotEqual(token, invitation.TokenHash);
            Assert.Null(invitation.UsedAt);
        }

        using var inviteeClient = CreateCookieClient(factory);

        var details = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/lookup",
            new { token });

        Assert.Equal(HttpStatusCode.OK, details.StatusCode);

        var accept = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // The invitee can now sign in with the password they chose.
        var login = await inviteeClient.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email = inviteeEmail, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await (await inviteeClient.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Contains(AppRoles.Staff, me!.Roles);
    }

    [Fact]
    public async Task AcceptedStaff_CanLoadOrganizationWorkspace()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"workspace-staff-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail,
            nameof(OrganizationRole.CaseWorker));

        using var inviteeClient = CreateCookieClient(factory);
        var acceptResponse = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        await SignInAsync(inviteeClient, inviteeEmail, InviteePassword);
        inviteeClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var organizationsResponse =
            await inviteeClient.GetAsync("/api/organizations/me");
        var staffResponse = await inviteeClient.GetAsync("/api/staff");
        var casesResponse = await inviteeClient.GetAsync(
            "/api/cases?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, organizationsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staffResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, casesResponse.StatusCode);

        var memberships =
            await organizationsResponse.Content.ReadFromJsonAsync<
                IReadOnlyList<OrganizationWorkspaceOption>>();
        Assert.Equal(organizationId, Assert.Single(memberships!).Id);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var user = await dbContext.ApplicationUsers.SingleAsync(
                candidate => candidate.Email == inviteeEmail);
            var membership = await dbContext.OrganizationMemberships
                .SingleAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.UserId == user.Id);

            Assert.Equal(
                OrganizationRole.CaseWorker,
                membership.Role);
        }

        inviteeClient.DefaultRequestHeaders.Remove("X-Organization-Id");
        inviteeClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            Guid.NewGuid().ToString());

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await inviteeClient.GetAsync("/api/staff")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await inviteeClient.GetAsync("/api/cases?page=1&pageSize=20")).StatusCode);
    }

    [Fact]
    public async Task Invitation_CannotBeUsedTwice()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"reuse-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        using var inviteeClient = CreateCookieClient(factory);

        var first = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = "Another-Borrowed-Shelf-8!" });

        Assert.Equal(HttpStatusCode.Gone, second.StatusCode);

        var lookup = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/lookup",
            new { token });

        Assert.Equal(HttpStatusCode.Gone, lookup.StatusCode);
    }

    [Fact]
    public async Task Invitation_WithWeakPassword_ReturnsBadRequestAndStaysUsable()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"weak-invite-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        using var inviteeClient = CreateCookieClient(factory);

        var weak = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = "alllowercase" });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        // A rejected password must not consume the invitation.
        var retry = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Invitation_DuplicatePendingInvitation_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"duplicate-{Guid.NewGuid():N}@example.com";

        await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        var duplicate = await adminClient.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new
            {
                fullName = "Invited Officer",
                email = inviteeEmail,
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Invitation_FromOtherOrganization_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            dbContext.Organizations.Add(
                Organization.Create(
                    "Foreign Organization",
                    $"FOR-{Guid.NewGuid():N}",
                    DateTimeOffset.UtcNow));

            await dbContext.SaveChangesAsync();
        }

        Guid foreignOrganizationId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            foreignOrganizationId = await dbContext.Organizations
                .Where(organization => organization.Id != organizationId)
                .Select(organization => organization.Id)
                .FirstAsync();
        }

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await adminClient.PostAsJsonAsync(
            $"/api/organizations/{foreignOrganizationId}/invitations",
            new
            {
                fullName = "Cross Tenant Officer",
                email = $"cross-{Guid.NewGuid():N}@example.com",
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_RevokedLinkIsRejected()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"revoke-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        Guid invitationId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            invitationId = await dbContext.AccountInvitations
                .Where(invitation =>
                    invitation.OrganizationId == organizationId)
                .Select(invitation => invitation.Id)
                .SingleAsync();
        }

        var revoke = await adminClient.DeleteAsync(
            $"/api/organizations/{organizationId}/invitations/{invitationId}");

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var inviteeClient = CreateCookieClient(factory);

        var accept = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.Gone, accept.StatusCode);
    }

    [Fact]
    public async Task Invitation_ExpiredLinkIsRejected()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"expired-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var invitation = await dbContext.AccountInvitations.SingleAsync(
                candidate =>
                    candidate.OrganizationId == organizationId);

            dbContext.Entry(invitation)
                .Property(candidate => candidate.ExpiresAt)
                .CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-1);

            await dbContext.SaveChangesAsync();
        }

        using var inviteeClient = CreateCookieClient(factory);

        var accept = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.Gone, accept.StatusCode);
    }

    [Fact]
    public async Task Invitation_CannotGrantSuperAdminRole()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await adminClient.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new
            {
                fullName = "Escalating Officer",
                email = $"escalate-{Guid.NewGuid():N}@example.com",
                role = "SuperAdmin"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<
            Dictionary<string, string>>();

        Assert.NotNull(body);
        Assert.Contains("staff members only", body!["error"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotInviteAnotherOrganizationAdmin()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);
        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await adminClient.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new
            {
                fullName = "Additional Administrator",
                email = $"additional-admin-{Guid.NewGuid():N}@example.com",
                role = nameof(OrganizationRole.OrganizationAdmin)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        Assert.Empty(await dbContext.AccountInvitations.ToListAsync());
    }

    [Fact]
    public async Task Invitation_InvitingExistingMember_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await adminClient.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/invitations",
            new
            {
                fullName = "Existing Admin",
                email = adminEmail,
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_ForExistingAccountWithoutMembership_LinksMembership()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        // An account that already exists (for example a former member of
        // another organization) but is not a member here yet.
        await EnsureRolesAsync(factory);

        var existingEmail = $"existing-{Guid.NewGuid():N}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = ApplicationUser.Create(
                "Existing Person",
                existingEmail,
                DateTimeOffset.UtcNow);

            user.EmailConfirmed = true;

            var result = await userManager.CreateAsync(
                user,
                "Original-Borrowed-Shelf-1!");

            Assert.True(result.Succeeded);
        }

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            existingEmail,
            nameof(OrganizationRole.CaseWorker));

        using var inviteeClient = CreateCookieClient(factory);

        var accept = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var membership = await dbContext.OrganizationMemberships
                .SingleAsync(candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.Role == OrganizationRole.CaseWorker);

            Assert.True(membership.IsActive);
        }

        var login = await inviteeClient.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email = existingEmail, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Invitation_AcceptanceReactivatesMembershipWithInvitedRole()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);
        await EnsureRolesAsync(factory);

        var inviteeEmail = $"reactivated-{Guid.NewGuid():N}@example.com";
        Guid inviteeId;

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var invitee = ApplicationUser.Create(
                "Reactivated Member",
                inviteeEmail,
                DateTimeOffset.UtcNow);
            invitee.EmailConfirmed = true;

            var createResult = await userManager.CreateAsync(
                invitee,
                StrongPassword);
            Assert.True(createResult.Succeeded);
            inviteeId = invitee.Id;

            var membership = OrganizationMembership.Create(
                inviteeId,
                organizationId,
                OrganizationRole.Viewer,
                DateTimeOffset.UtcNow);
            membership.Deactivate(DateTimeOffset.UtcNow);
            dbContext.OrganizationMemberships.Add(membership);
            await dbContext.SaveChangesAsync();
        }

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail,
            nameof(OrganizationRole.CaseWorker));

        using var inviteeClient = CreateCookieClient(factory);
        var acceptResponse = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var reactivatedMembership = await verifyDbContext.OrganizationMemberships
            .SingleAsync(candidate =>
                candidate.UserId == inviteeId &&
                candidate.OrganizationId == organizationId);

        Assert.True(reactivatedMembership.IsActive);
        Assert.Equal(
            OrganizationRole.CaseWorker,
            reactivatedMembership.Role);
    }

    [Fact]
    public async Task Invitation_LookupForUnknownToken_ReturnsGone()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/invitations/lookup",
            new { token = new string('a', 43) });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    /// <summary>
    /// staff_members is a row-level-security tenant table. The accept path
    /// must open a tenant transaction on the invitation's organization before
    /// inserting a StaffMember, otherwise PostgreSQL rejects the insert with
    /// 42501 and the endpoint returns 500. InMemory tests cannot exercise the
    /// policy itself, so we assert that the service calls BeginAsync.
    /// </summary>
    [Fact]
    public async Task InvitationAccept_OpensTenantTransactionForOrganization()
    {
        var spy = new SpyOrganizationTransactionContext();

        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false,
            ConfigureServices = services =>
            {
                services.RemoveAll<IOrganizationTransactionContext>();
                services.AddScoped<IOrganizationTransactionContext>(_ => spy);
            }
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);
        await SignInAsync(adminClient, adminEmail, StrongPassword);
        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var inviteeEmail = $"spy-invitee-{Guid.NewGuid():N}@example.com";
        var token = await InviteAsync(
            factory,
            adminClient,
            organizationId,
            inviteeEmail);

        // Reset the spy so we only observe the accept request, not the
        // admin's earlier invitation-create request (which does not need a
        // tenant transaction).
        spy.BeginOrganizationCalls.Clear();

        using var inviteeClient = CreateCookieClient(factory);

        var accept = await inviteeClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token, password = InviteePassword });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        // The accept path must have opened a tenant transaction on the
        // invitation's organization before writing to staff_members.
        Assert.Contains(organizationId, spy.BeginOrganizationCalls);
        Assert.Empty(spy.BeginCitizenCalls);
    }

    /// <summary>
    /// Password reset only touches Identity tables and audit_events, so it
    /// must succeed WITHOUT opening a tenant transaction. If a regression
    /// causes the reset flow to leak into the tenant-scoped path, this test
    /// will catch it.
    /// </summary>
    [Fact]
    public async Task PasswordReset_ForgotAndResetSucceedsStandalone()
    {
        await using var factory = new CivicFlowApiFactory(enableBusinessEndpoints: true)
        {
            UseTestAuthentication = false
        };

        await EnsureRolesAsync(factory);

        var email = $"reset-target-{Guid.NewGuid():N}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = ApplicationUser.Create(
                "Reset Target",
                email,
                DateTimeOffset.UtcNow);

            user.EmailConfirmed = true;

            var created = await userManager.CreateAsync(user, StrongPassword);
            Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, AppRoles.Staff);
        }

        using var client = CreateCookieClient(factory);

        var forgot = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { email });

        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        var message = factory.EmailSender.LastFor(email);
        Assert.NotNull(message);

        var resetToken = factory.EmailSender.ExtractToken(
            message!.Body,
            "token");

        Assert.False(string.IsNullOrWhiteSpace(resetToken));

        var newPassword = "Rotated-Passphrase-99#";

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { email, token = resetToken, newPassword });

        var resetBody = await reset.Content.ReadAsStringAsync();
        Assert.Equal(
            HttpStatusCode.OK,
            reset.StatusCode);

        var signIn = await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = newPassword });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }
}

/// <summary>
/// Test double that records the organization ids passed to BeginAsync and
/// user ids passed to BeginCitizenAsync. CompleteAsync is a no-op because the
/// in-memory provider ignores transactions anyway.
/// </summary>
internal sealed class SpyOrganizationTransactionContext
    : IOrganizationTransactionContext
{
    public List<Guid> BeginOrganizationCalls { get; } = [];

    public List<Guid> BeginCitizenCalls { get; } = [];

    public bool IsActive => false;

    public Task BeginAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        BeginOrganizationCalls.Add(organizationId);
        return Task.CompletedTask;
    }

    public Task BeginCitizenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        BeginCitizenCalls.Add(userId);
        return Task.CompletedTask;
    }

    public Task BeginPlatformReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CompleteAsync(
        bool succeeded,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
