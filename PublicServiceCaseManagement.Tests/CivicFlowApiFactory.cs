
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
    private readonly bool useAuthenticatedOrganizationContext;

    public CivicFlowApiFactory(bool useAuthenticatedOrganizationContext = false)
    {
        this.useAuthenticatedOrganizationContext = useAuthenticatedOrganizationContext;
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
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme =
                        TestAuthenticationHandler.SchemeName;
                });

            services.RemoveAll<
                IDbContextOptionsConfiguration<CivicFlowDbContext>>();

            services.RemoveAll<
                DbContextOptions<CivicFlowDbContext>>();

            services.RemoveAll<CivicFlowDbContext>();

            services.AddDbContext<CivicFlowDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            if (useAuthenticatedOrganizationContext)
            {
                services.RemoveAll<ICurrentOrganizationContext>();

                services.AddScoped<
                    ICurrentOrganizationContext,
                    AuthenticatedCurrentOrganizationContext>();
            }
        });
    }

    public async Task<Guid> SeedAuthenticatedOrganizationAsync()
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

        var organization = Organization.Create(
            "Integration Test Organization",
            $"INT-{Guid.NewGuid():N}",
            now);

        var membership = OrganizationMembership.Create(
            user.Id,
            organization.Id,
            OrganizationRole.CaseWorker,
            now);

        dbContext.ApplicationUsers.Add(user);
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(membership);

        await dbContext.SaveChangesAsync();

        return organization.Id;
    }
}
