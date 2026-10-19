
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests;

public class CivicFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"CivicFlowTests_{Guid.NewGuid():N}";
    private readonly OrganizationRole? defaultOrganizationRole;
    private readonly PlatformRole defaultPlatformRole;

    private static readonly Guid DevelopmentOrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public CivicFlowApiFactory(bool useAuthenticatedOrganizationContext = false)
    {
        _ = useAuthenticatedOrganizationContext;
    }

    public CivicFlowApiFactory(
        OrganizationRole defaultOrganizationRole,
        PlatformRole defaultPlatformRole = PlatformRole.None)
    {
        this.defaultOrganizationRole = defaultOrganizationRole;
        this.defaultPlatformRole = defaultPlatformRole;
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
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CivicFlow"] =
                        "Host=localhost;Database=civicflow_tests;Username=test;Password=test"
                });
        });

        builder.ConfigureServices(services =>
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

            services.RemoveAll<IDbContextOptionsConfiguration<CivicFlowDbContext>>();
            services.RemoveAll<DbContextOptions<CivicFlowDbContext>>();
            services.RemoveAll<CivicFlowDbContext>();

            services.AddDbContext<CivicFlowDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

        });
    }

    private async Task SeedDefaultDevelopmentIdentityAsync(
        OrganizationRole role,
        PlatformRole platformRole)
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        var organization = await dbContext.Organizations
            .SingleAsync(item => item.Id == DevelopmentOrganizationId);

        var now = DateTimeOffset.UtcNow;
        var user = ApplicationUser.Create(
            "CivicFlow Development Test User",
            $"development-{Guid.NewGuid():N}@example.com",
            now);

        user.LinkExternalIdentity(
            TestAuthenticationHandler.Issuer,
            TestAuthenticationHandler.Subject,
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

        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;

        var user = ApplicationUser.Create(
            "CivicFlow Integration Test User",
            $"integration-{Guid.NewGuid():N}@example.com",
            now);

        user.LinkExternalIdentity(
            TestAuthenticationHandler.Issuer,
            TestAuthenticationHandler.Subject,
            now);

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

        dbContext.ApplicationUsers.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        return organization.Id;
    }

    public async Task<Guid> SeedAdditionalOrganizationForAuthenticatedUserAsync(
        OrganizationRole role = OrganizationRole.OrganizationAdmin)
    {
        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var user = await dbContext.ApplicationUsers.SingleAsync(
            user =>
                user.ExternalIdentityIssuer == TestAuthenticationHandler.Issuer &&
                user.ExternalIdentitySubject == TestAuthenticationHandler.Subject);

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
