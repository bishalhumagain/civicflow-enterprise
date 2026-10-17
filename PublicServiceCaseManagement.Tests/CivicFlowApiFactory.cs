using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        builder.ConfigureServices(services =>
        {
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