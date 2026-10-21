
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Infrastructure.Organizations;

public sealed class AuthenticatedOrganizationResolverTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResolveAsync_WithActiveMembership_ReturnsOrganizationId()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Equal(scenario.OrganizationId, result);
    }

    [Fact]
    public async Task ResolveAsync_WithUnlinkedUser_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync(knownUser: false);

        await using var dbContext = scenario.DbContext;

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithoutOrganizationHeader_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        scenario.HttpContext.Request.Headers.Remove("X-Organization-Id");

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithInvalidOrganizationHeader_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        scenario.HttpContext.Request.Headers["X-Organization-Id"] =
            "not-a-guid";

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithInactiveMembership_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync(activeMembership: false);

        await using var dbContext = scenario.DbContext;

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithInactiveApplicationUser_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        var user = await dbContext.ApplicationUsers
            .SingleAsync(item => item.Id == scenario.UserId);
        user.Deactivate(Now.AddMinutes(1));
        await dbContext.SaveChangesAsync();

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithOrganizationWithoutMembership_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        var otherOrganization = Organization.Create(
            "Other Test Organization",
            $"OTHER-{Guid.NewGuid():N}",
            Now);

        dbContext.Organizations.Add(otherOrganization);
        await dbContext.SaveChangesAsync();

        scenario.HttpContext.Request.Headers["X-Organization-Id"] =
            otherOrganization.Id.ToString();

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithInactiveOrganization_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync(activeOrganization: false);

        await using var dbContext = scenario.DbContext;

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithUnauthenticatedIdentity_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync(authenticated: false);

        await using var dbContext = scenario.DbContext;

        var result = await scenario.Resolver.ResolveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePlatformRoleAsync_WithLinkedSuperAdmin_ReturnsSuperAdmin()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        var user = await dbContext.ApplicationUsers
            .SingleAsync(user => user.Id == scenario.UserId);

        user.SetPlatformRole(PlatformRole.SuperAdmin, Now);
        await dbContext.SaveChangesAsync();

        var result = await scenario.Resolver.ResolvePlatformRoleAsync();

        Assert.Equal(PlatformRole.SuperAdmin, result);
    }

    [Fact]
    public async Task ResolvePlatformRoleAsync_WithUnlinkedIdentity_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync(knownUser: false);

        await using var dbContext = scenario.DbContext;

        var result = await scenario.Resolver.ResolvePlatformRoleAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolvePlatformRoleAsync_WithInactiveUser_ReturnsNull()
    {
        var scenario = await CreateScenarioAsync();

        await using var dbContext = scenario.DbContext;

        var user = await dbContext.ApplicationUsers
            .SingleAsync(item => item.Id == scenario.UserId);
        user.SetPlatformRole(PlatformRole.SuperAdmin, Now);
        user.Deactivate(Now.AddMinutes(1));
        await dbContext.SaveChangesAsync();

        var result = await scenario.Resolver.ResolvePlatformRoleAsync();

        Assert.Null(result);
    }

    private static async Task<ResolverScenario> CreateScenarioAsync(
        bool knownUser = true,
        bool activeMembership = true,
        bool activeOrganization = true,
        bool authenticated = true)
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"ResolverTests_{Guid.NewGuid():N}")
            .Options;

        var dbContext = new CivicFlowDbContext(options);

        await dbContext.Database.EnsureCreatedAsync();

        var user = ApplicationUser.Create(
            "Resolver Test User",
            $"resolver-{Guid.NewGuid():N}@example.com",
            Now);

        var organization = Organization.Create(
            "Resolver Test Organization",
            $"RES-{Guid.NewGuid():N}",
            Now);

        if (!activeOrganization)
        {
            organization.Deactivate(Now.AddMinutes(1));
        }

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.Viewer,
            Now);

        if (!activeMembership)
        {
            membership.Deactivate(Now.AddMinutes(1));
        }

        dbContext.ApplicationUsers.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();

        // When the caller is a "known user" the authenticated principal
        // carries the seeded user id; otherwise it carries an unrelated id
        // that does not exist in the database.
        var authenticatedUserId = knownUser
            ? user.Id
            : Guid.NewGuid();

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    authenticatedUserId.ToString())
            },
            authenticated ? "ResolverTest" : null);

        httpContext.User = new ClaimsPrincipal(identity);
        httpContext.Request.Headers["X-Organization-Id"] =
            organization.Id.ToString();

        var accessor = new FixedHttpContextAccessor(httpContext);

        var resolver = new AuthenticatedOrganizationResolver(
            accessor,
            dbContext);

        return new ResolverScenario(
            dbContext,
            resolver,
            httpContext,
            user.Id,
            organization.Id);
    }

    private sealed class FixedHttpContextAccessor(
        HttpContext httpContext) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = httpContext;
    }

    private sealed record ResolverScenario(
        CivicFlowDbContext DbContext,
        AuthenticatedOrganizationResolver Resolver,
        DefaultHttpContext HttpContext,
        Guid UserId,
        Guid OrganizationId);
}
