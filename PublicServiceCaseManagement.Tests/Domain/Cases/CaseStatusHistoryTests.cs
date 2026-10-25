
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Tests.Domain.Cases;

public sealed class CaseStatusHistoryTests
{
    private static readonly Guid CaseId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset ChangedAt =
        new(2026, 10, 17, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_CreatesHistoryRecord()
    {
        var history = CaseStatusHistory.Create(
            CaseId,
            OrganizationId,
            CaseStatus.Submitted,
            CaseStatus.UnderReview,
            ChangedAt);

        Assert.NotEqual(Guid.Empty, history.Id);
        Assert.Equal(CaseId, history.CaseId);
        Assert.Equal(OrganizationId, history.OrganizationId);
        Assert.Equal(CaseStatus.Submitted, history.PreviousStatus);
        Assert.Equal(CaseStatus.UnderReview, history.NewStatus);
        Assert.Equal(ChangedAt, history.ChangedAt);
        Assert.Null(history.ChangedByUserId);
    }

    [Fact]
    public void Create_WithEmptyCaseId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CaseStatusHistory.Create(
                Guid.Empty,
                OrganizationId,
                CaseStatus.Submitted,
                CaseStatus.UnderReview,
                ChangedAt));
    }

    [Fact]
    public void Create_WithEmptyOrganizationId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CaseStatusHistory.Create(
                CaseId,
                Guid.Empty,
                CaseStatus.Submitted,
                CaseStatus.UnderReview,
                ChangedAt));
    }

    [Fact]
    public void Create_WithSamePreviousAndNewStatus_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CaseStatusHistory.Create(
                CaseId,
                OrganizationId,
                CaseStatus.Submitted,
                CaseStatus.Submitted,
                ChangedAt));
    }

    [Fact]
    public void Create_WithExplanationAndVisibility_StoresBoth()
    {
        var history = CaseStatusHistory.Create(
            CaseId,
            OrganizationId,
            CaseStatus.Submitted,
            CaseStatus.UnderReview,
            ChangedAt,
            changedByUserId: null,
            explanation: "  Review started.  ",
            isVisibleToCitizen: true);

        Assert.Equal("Review started.", history.Explanation);
        Assert.True(history.IsVisibleToCitizen);
    }

    [Fact]
    public void Create_WithExplanationThatIsNotVisible_HidesItFromCitizens()
    {
        var history = CaseStatusHistory.Create(
            CaseId,
            OrganizationId,
            CaseStatus.Submitted,
            CaseStatus.UnderReview,
            ChangedAt,
            explanation: "Internal rationale.",
            isVisibleToCitizen: false);

        Assert.Equal("Internal rationale.", history.Explanation);
        Assert.False(history.IsVisibleToCitizen);
    }

    [Fact]
    public void Create_WithTooLongExplanation_Throws()
    {
        Assert.Throws<ArgumentException>(() => CaseStatusHistory.Create(
            CaseId,
            OrganizationId,
            CaseStatus.Submitted,
            CaseStatus.UnderReview,
            ChangedAt,
            explanation: new string(
                'x',
                CaseStatusHistory.MaximumExplanationLength + 1),
            isVisibleToCitizen: true));
    }
}
