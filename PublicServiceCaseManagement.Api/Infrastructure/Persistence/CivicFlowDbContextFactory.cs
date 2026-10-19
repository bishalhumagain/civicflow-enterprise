using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PublicServiceCaseManagement.Api.Infrastructure.Persistence;

public sealed class CivicFlowDbContextFactory
    : IDesignTimeDbContextFactory<CivicFlowDbContext>
{
    public CivicFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionArgument(args)
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__CivicFlow");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration.GetConnectionString("CivicFlow");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A CivicFlow connection string is required for EF Core design-time operations. " +
                "Use --connection or ConnectionStrings__CivicFlow.");
        }

        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CivicFlowDbContext(options);
    }

    private static string? GetConnectionArgument(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--connection", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
