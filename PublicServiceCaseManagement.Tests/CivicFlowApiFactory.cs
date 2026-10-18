
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests;

public class CivicFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName =
        $"CivicFlowTests_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
        });
    }
}
