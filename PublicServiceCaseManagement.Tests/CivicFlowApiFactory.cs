
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests;

public class CivicFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"CivicFlowTests_{Guid.NewGuid():N}";
    private readonly OrganizationRole? defaultOrganizationRole;
    private readonly PlatformRole defaultPlatformRole;
    private readonly string environmentName;
    private readonly bool enableBusinessEndpoints;
    private readonly bool requireAntiforgery;

    public CapturingEmailSender EmailSender { get; } = new();

    /// <summary>
    /// When true (default) the test authentication scheme is installed as the
    /// default scheme so existing integration tests can drive authorization
    /// with headers. When false the real ASP.NET Core Identity cookie scheme
    /// is left in place, which is required for end-to-end auth flow tests.
    /// </summary>
    public bool UseTestAuthentication { get; set; } = true;

    private static readonly Guid DevelopmentOrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public CivicFlowApiFactory(
        bool useAuthenticatedOrganizationContext = false,
        string environmentName = "Development",
        bool enableBusinessEndpoints = false,
        bool requireAntiforgery = false)
    {
        _ = useAuthenticatedOrganizationContext;
        this.environmentName = environmentName;
        this.enableBusinessEndpoints = enableBusinessEndpoints;
        this.requireAntiforgery = requireAntiforgery;
    }

    public CivicFlowApiFactory(
        OrganizationRole defaultOrganizationRole,
        PlatformRole defaultPlatformRole = PlatformRole.None,
        string environmentName = "Development",
        bool enableBusinessEndpoints = false,
        bool requireAntiforgery = false)
    {
        this.defaultOrganizationRole = defaultOrganizationRole;
        this.defaultPlatformRole = defaultPlatformRole;
        this.environmentName = environmentName;
        this.enableBusinessEndpoints = enableBusinessEndpoints;
        this.requireAntiforgery = requireAntiforgery;
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();

        if (defaultOrganizationRole.HasValue)
        {
            SeedDefaultDevelopmentIdentityAsync(
                    defaultOrganizationRole.Value,
                    defaultPlatformRole)
                .GetAwaiter()
                .GetResult();

            client.DefaultRequestHeaders.Add(
                "X-Organization-Id",
                DevelopmentOrganizationId.ToString());
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseSetting(
            "ConnectionStrings:CivicFlow",
            "Host=localhost;Database=civicflow_tests;Username=test");
        builder.UseSetting(
            "BusinessEndpoints:Enabled",
            enableBusinessEndpoints.ToString());

        // Anti-forgery is disabled for the general integration-test factory
        // because the test authentication scheme bypasses the browser cookie
        // flow. Dedicated tests re-enable it to prove CSRF protection works.
        builder.UseSetting(
            "Security:RequireAntiforgery",
            requireAntiforgery ? "true" : "false");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CivicFlow"] =
                        "Host=localhost;Database=civicflow_tests;Username=test",
                    ["BusinessEndpoints:Enabled"] =
                        enableBusinessEndpoints.ToString(),
                    ["Security:RequireAntiforgery"] =
                        requireAntiforgery ? "true" : "false",
                    ["FileStorage:RootPath"] =
                        Path.Combine(Path.GetTempPath(), databaseName)
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            if (UseTestAuthentication)
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });

                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(
                    options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                    });
            }

            services.RemoveAll<IDbContextOptionsConfiguration<CivicFlowDbContext>>();
            services.RemoveAll<DbContextOptions<CivicFlowDbContext>>();
            services.RemoveAll<CivicFlowDbContext>();

            services.AddDbContext<CivicFlowDbContext>(options =>
                options.UseInMemoryDatabase(databaseName)
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        });
    }

    private async Task EnsureDatabaseAsync(CivicFlowDbContext dbContext)
    {
        await dbContext.Database.EnsureCreatedAsync();

        // In-memory databases created by EnsureCreated do not run the
        // migration seeders, so make sure the role catalogue exists.
        var roleManager = Services
            .CreateScope()
            .ServiceProvider
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

    private static ApplicationUser CreateIntegrationTestUser(
        string fullName,
        DateTimeOffset now)
    {
        // The test authentication handler always authenticates as the
        // well-known default user id, so align seeded users with it.
        var user = ApplicationUser.Create(
            fullName,
            $"integration-{Guid.NewGuid():N}@example.com",
            now);

        user.Id = TestAuthenticationHandler.DefaultUserId;
        return user;
    }

    private async Task SeedDefaultDevelopmentIdentityAsync(
        OrganizationRole role,
        PlatformRole platformRole)
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        await EnsureDatabaseAsync(dbContext);

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(item => item.Id == DevelopmentOrganizationId);

        if (organization is null)
        {
            organization = Organization.Create(
                "CivicFlow Development Organization",
                "CIVICFLOW-DEV",
                DateTimeOffset.UtcNow);

            typeof(Organization)
                .GetProperty(nameof(Organization.Id))!
                .SetValue(organization, DevelopmentOrganizationId);

            dbContext.Organizations.Add(organization);
            await dbContext.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var user = CreateIntegrationTestUser(
            "CivicFlow Development Test User",
            now);

        user.SetPlatformRole(platformRole, now);

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            role,
            now);

        dbContext.ApplicationUsers.Add(user);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();
    }

    public async Task<Guid> SeedAuthenticatedOrganizationAsync(
        OrganizationRole role = OrganizationRole.CaseWorker,
        PlatformRole platformRole = PlatformRole.None)
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        await EnsureDatabaseAsync(dbContext);

        var now = DateTimeOffset.UtcNow;

        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == TestAuthenticationHandler.DefaultUserId);

        if (user is null)
        {
            user = CreateIntegrationTestUser(
                "CivicFlow Integration Test User",
                now);
            dbContext.ApplicationUsers.Add(user);
        }

        user.SetPlatformRole(platformRole, now);

        var organization = Organization.Create(
            "Integration Test Organization",
            $"INT-{Guid.NewGuid():N}",
            now);

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            role,
            now);

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        return organization.Id;
    }

    public async Task<Guid> SeedAuthenticatedPlatformUserAsync(
        PlatformRole platformRole = PlatformRole.None)
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        await EnsureDatabaseAsync(dbContext);

        var now = DateTimeOffset.UtcNow;

        var user = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == TestAuthenticationHandler.DefaultUserId);

        if (user is null)
        {
            user = CreateIntegrationTestUser(
                "CivicFlow Citizen Test User",
                now);
            dbContext.ApplicationUsers.Add(user);
        }

        user.SetPlatformRole(platformRole, now);

        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    public async Task<Guid> SeedAdditionalOrganizationForAuthenticatedUserAsync(
        OrganizationRole role = OrganizationRole.OrganizationAdmin)
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var user = await dbContext.ApplicationUsers.SingleAsync(
            candidate =>
                candidate.Id == TestAuthenticationHandler.DefaultUserId);

        var now = DateTimeOffset.UtcNow;

        var organization = Organization.Create(
            "Second Integration Test Organization",
            $"INT-{Guid.NewGuid():N}",
            now);

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            role,
            now);

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        return organization.Id;
    }

    public async Task<Guid> SeedOrganizationWithoutMembershipAsync()
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var now = DateTimeOffset.UtcNow;

        var organization = Organization.Create(
            "Organization Without Membership",
            $"NOM-{Guid.NewGuid():N}",
            now);

        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();

        return organization.Id;
    }
}
