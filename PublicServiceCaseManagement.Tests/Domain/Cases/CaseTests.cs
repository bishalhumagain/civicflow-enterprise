
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Tests.Domain.Cases;

public sealed class CaseTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset Now =
        new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private static Case CreateCase() =>
        Case.Create(
            OrganizationId,
            "CF-12345678",
            " Damaged streetlight ",
            " A streetlight near the public park is damaged. ",
            Now);

    [Fact]
    public void Create_SetsDefaultsAndTrimsDetails()
    {
        var newCase = CreateCase();

        Assert.NotEqual(Guid.Empty, newCase.Id);
        Assert.Equal(OrganizationId, newCase.OrganizationId);
        Assert.Equal("Damaged streetlight", newCase.Title);
        Assert.Equal(
            "A streetlight near the public park is damaged.",
            newCase.Description);
        Assert.Equal(CaseStatus.Submitted, newCase.Status);
        Assert.Equal(CasePriority.Normal, newCase.Priority);
        Assert.Equal(Now, newCase.CreatedAt);
        Assert.Equal(Now, newCase.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ToUnderReview_UpdatesStatus()
    {
        var newCase = CreateCase();
        var updatedAt = Now.AddMinutes(10);

        newCase.ChangeStatus(CaseStatus.UnderReview, updatedAt);

        Assert.Equal(CaseStatus.UnderReview, newCase.Status);
        Assert.Equal(updatedAt, newCase.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_ToResolvedFromSubmitted_Throws()
    {
        var newCase = CreateCase();

        Assert.Throws<InvalidOperationException>(
            () => newCase.ChangeStatus(CaseStatus.Resolved, Now));
    }

    [Fact]
    public void UpdateDetails_TrimsAndUpdatesTimestamp()
    {
        var newCase = CreateCase();
        var updatedAt = Now.AddMinutes(15);

        newCase.UpdateDetails(
            " Updated streetlight title ",
            " Updated description for the damaged streetlight. ",
            updatedAt);

        Assert.Equal("Updated streetlight title", newCase.Title);
        Assert.Equal(
            "Updated description for the damaged streetlight.",
            newCase.Description);
        Assert.Equal(updatedAt, newCase.UpdatedAt);
    }

    [Fact]
    public void ChangePriority_ToHigh_UpdatesPriority()
    {
        var newCase = CreateCase();
        var updatedAt = Now.AddMinutes(5);

        newCase.ChangePriority(CasePriority.High, updatedAt);

        Assert.Equal(CasePriority.High, newCase.Priority);
        Assert.Equal(updatedAt, newCase.UpdatedAt);
    }

    [Fact]
    public void Create_WithBlankTitle_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => Case.Create(
                OrganizationId,
                "CF-12345678",
                " ",
                "A valid description for this case.",
                Now));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("  a  ")]
    [InlineData("   ")]
    public void Create_WithTitleShorterThanMinimumAfterNormalization_Throws(
        string title)
    {
        Assert.Throws<ArgumentException>(
            () => Case.Create(
                OrganizationId,
                "CF-12345678",
                title,
                "A valid description for this case.",
                Now));
    }

    [Theory]
    [InlineData("too short")]
    [InlineData("  123456789  ")]
    [InlineData("\n\n123456789\n\n")]
    public void Create_WithDescriptionShorterThanMinimumAfterTrimming_Throws(
        string description)
    {
        Assert.Throws<ArgumentException>(
            () => Case.Create(
                OrganizationId,
                "CF-12345678",
                "Valid title",
                description,
                Now));
    }

    [Fact]
    public void Create_WithMinimumLengthDetails_Succeeds()
    {
        var newCase = Case.Create(
            OrganizationId,
            "CF-12345678",
            "abc",
            "0123456789",
            Now);

        Assert.Equal("abc", newCase.Title);
        Assert.Equal("0123456789", newCase.Description);
    }

    [Fact]
    public void UpdateDetails_WhenClosed_Throws()
    {
        var newCase = CreateCase();

        newCase.ChangeStatus(CaseStatus.UnderReview, Now);
        newCase.ChangeStatus(CaseStatus.Assigned, Now);
        newCase.ChangeStatus(CaseStatus.InProgress, Now);
        newCase.ChangeStatus(CaseStatus.Resolved, Now);
        newCase.ChangeStatus(CaseStatus.Closed, Now);

        Assert.Throws<InvalidOperationException>(
            () => newCase.UpdateDetails(
                "New title",
                "A valid updated description.",
                Now.AddMinutes(5)));
    }
}
