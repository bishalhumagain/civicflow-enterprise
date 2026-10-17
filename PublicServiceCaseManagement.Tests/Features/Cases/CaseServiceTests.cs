
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests.Features.Cases;

public sealed class CaseServiceTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset Now =
        new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private static CivicFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CivicFlowDbContext(options);
    }

    private static Case CreateCase() =>
        Case.Create(
            OrganizationId,
            "CF-12345678",
            "Damaged streetlight",
            "A streetlight near the public park is damaged.",
            Now);

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusChanges_SavesCaseAndHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = new CaseService(dbContext);

        var result = await service.ChangeStatusAsync(
            caseEntity.Id,
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.NotNull(result);
        Assert.Equal(CaseStatus.UnderReview, result.Status);

        var history = await dbContext.CaseStatusHistories
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(caseEntity.Id, history.CaseId);
        Assert.Equal(OrganizationId, history.OrganizationId);
        Assert.Equal(CaseStatus.Submitted, history.PreviousStatus);
        Assert.Equal(CaseStatus.UnderReview, history.NewStatus);
        Assert.Null(history.ChangedByUserId);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCaseDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var service = new CaseService(dbContext);

        var result = await service.ChangeStatusAsync(
            Guid.NewGuid(),
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.Null(result);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenTransitionIsInvalid_DoesNotSaveHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = new CaseService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeStatusAsync(
                caseEntity.Id,
                OrganizationId,
                CaseStatus.Resolved));

        Assert.Equal(CaseStatus.Submitted, caseEntity.Status);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusIsUnchanged_DoesNotCreateHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();

        caseEntity.ChangeStatus(CaseStatus.UnderReview, Now);

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = new CaseService(dbContext);

        var result = await service.ChangeStatusAsync(
            caseEntity.Id,
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.NotNull(result);
        Assert.Equal(CaseStatus.UnderReview, result.Status);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
    }

}
