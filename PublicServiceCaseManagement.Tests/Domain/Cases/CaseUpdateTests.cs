using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Tests.Domain.Cases;

public sealed class CaseUpdateTests
{
    private static readonly Guid CaseId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid AuthorUserId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CaseUpdateKind.PublicUpdate)]
    [InlineData(CaseUpdateKind.InformationRequest)]
    [InlineData(CaseUpdateKind.CitizenResponse)]
    public void Create_WithCitizenFacingKind_IsVisibleToCitizen(
        CaseUpdateKind kind)
    {
        var update = CaseUpdate.Create(
            CaseId,
            OrganizationId,
            kind,
            "  A useful update.  ",
            AuthorUserId,
            CreatedAt);

        Assert.NotEqual(Guid.Empty, update.Id);
        Assert.Equal(CaseId, update.CaseId);
        Assert.Equal(OrganizationId, update.OrganizationId);
        Assert.Equal(kind, update.Kind);
        Assert.Equal("A useful update.", update.Message);
        Assert.True(update.IsVisibleToCitizen);
        Assert.Equal(AuthorUserId, update.AuthorUserId);
        Assert.Equal(CreatedAt, update.CreatedAt);
    }

    [Fact]
    public void Create_WithInternalNote_IsNotVisibleToCitizen()
    {
        var update = CaseUpdate.Create(
            CaseId,
            OrganizationId,
            CaseUpdateKind.InternalNote,
            "Staff-only context.",
            AuthorUserId,
            CreatedAt);

        Assert.False(update.IsVisibleToCitizen);
    }

    [Fact]
    public void Create_WithEmptyMessage_Throws()
    {
        Assert.Throws<ArgumentException>(() => CaseUpdate.Create(
            CaseId,
            OrganizationId,
            CaseUpdateKind.PublicUpdate,
            "   ",
            AuthorUserId,
            CreatedAt));
    }

    [Fact]
    public void Create_WithTooLongMessage_Throws()
    {
        Assert.Throws<ArgumentException>(() => CaseUpdate.Create(
            CaseId,
            OrganizationId,
            CaseUpdateKind.PublicUpdate,
            new string('x', CaseUpdate.MaximumMessageLength + 1),
            AuthorUserId,
            CreatedAt));
    }

    [Fact]
    public void Create_WithEmptyIds_Throws()
    {
        Assert.Throws<ArgumentException>(() => CaseUpdate.Create(
            Guid.Empty,
            OrganizationId,
            CaseUpdateKind.PublicUpdate,
            "Message",
            AuthorUserId,
            CreatedAt));

        Assert.Throws<ArgumentException>(() => CaseUpdate.Create(
            CaseId,
            Guid.Empty,
            CaseUpdateKind.PublicUpdate,
            "Message",
            AuthorUserId,
            CreatedAt));

        Assert.Throws<ArgumentException>(() => CaseUpdate.Create(
            CaseId,
            OrganizationId,
            CaseUpdateKind.PublicUpdate,
            "Message",
            Guid.Empty,
            CreatedAt));
    }

    [Fact]
    public void Create_WithUnknownKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseUpdate.Create(
            CaseId,
            OrganizationId,
            (CaseUpdateKind)99,
            "Message",
            AuthorUserId,
            CreatedAt));
    }
}
