using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PublicServiceCaseManagement.Api.Domain.Errors;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Infrastructure;

/// <summary>
/// The error logger runs after a request has already failed inside the same
/// scoped DbContext. If a prior SaveChanges threw (for example a rejected
/// staff_members insert under RLS), the change tracker still holds the
/// offending entities, and a naive SaveChanges retries the same failing write.
/// These tests prove the error row is still persisted in that situation.
/// </summary>
public sealed class ErrorLoggerTests
{
    private static CivicFlowDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase($"ErrorLogger_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId
                    .TransactionIgnoredWarning))
            .Options;

        return new CivicFlowDbContext(options);
    }

    [Fact]
    public async Task LogErrorAsync_PersistsErrorRow()
    {
        await using var dbContext = CreateContext();
        var logger = new ErrorLogger(
            dbContext,
            NullLogger<ErrorLogger>.Instance);

        await logger.LogErrorAsync(
            new InvalidOperationException("first failure"),
            userId: null,
            organizationId: null,
            additionalData: "unit-test");

        var row = await dbContext.ApplicationErrors.SingleAsync();
        Assert.Equal("first failure", row.Message);
        Assert.Equal("unit-test", row.AdditionalData);
        Assert.False(row.IsHandled);
    }

    [Fact]
    public async Task LogErrorAsync_DetachesPendingWritesFromFailedSave()
    {
        await using var dbContext = CreateContext();

        // Simulate the change tracker holding an unsaved entity that a prior
        // SaveChanges rejected. InMemory cannot enforce RLS, so we detach it
        // manually via the error logger path: the logger should still persist
        // its own row without re-attempting the rejected write.
        var organization = Organization.Create(
            "Pending Org",
            $"PENDING-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        dbContext.Organizations.Add(organization);
        // Do NOT call SaveChanges; leave the entity tracked and Added.

        var logger = new ErrorLogger(
            dbContext,
            NullLogger<ErrorLogger>.Instance);

        await logger.LogErrorAsync(
            new Exception("simulated RLS rejection"),
            organizationId: organization.Id);

        // The ApplicationError must exist.
        var error = await dbContext.ApplicationErrors.SingleAsync();
        Assert.Equal("simulated RLS rejection", error.Message);

        // The rejected organization insert must have been detached, not
        // committed as part of the error-log SaveChanges.
        var stillTracked = await dbContext.Organizations
            .AnyAsync(item => item.Id == organization.Id);
        Assert.False(stillTracked);
    }
}
