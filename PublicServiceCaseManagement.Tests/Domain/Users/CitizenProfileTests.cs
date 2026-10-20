using PublicServiceCaseManagement.Api.Domain.Users;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Domain.Users;

public sealed class CitizenProfileTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    private static readonly Guid UserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Create_TrimsValuesAndAssignsId()
    {
        var profile = CitizenProfile.Create(
            UserId,
            "  Bishal Humagain  ",
            "  bishal@example.com  ",
            "  9800000000  ",
            "  123456789  ",
            "  12-34-5678  ",
            30,
            "  Male  ",
            "  Kathmandu  ",
            "  Lalitpur  ",
            Now);

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal(UserId, profile.ApplicationUserId);
        Assert.Equal("Bishal Humagain", profile.FullName);
        Assert.Equal("bishal@example.com", profile.Email);
        Assert.Equal("9800000000", profile.PhoneNumber);
        Assert.Equal("123456789", profile.NIDNumber);
        Assert.Equal("12-34-5678", profile.CitizenshipNumber);
        Assert.Equal(30, profile.Age);
        Assert.Equal("Male", profile.Gender);
        Assert.Equal("Kathmandu", profile.PermanentAddress);
        Assert.Equal("Lalitpur", profile.TemporaryAddress);
        Assert.Equal(Now, profile.CreatedAt);
        Assert.Equal(Now, profile.UpdatedAt);
    }

    [Fact]
    public void Create_WithEmptyApplicationUserId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CitizenProfile.Create(
                Guid.Empty,
                "Valid Name",
                "valid@example.com",
                null, null, null, null, null, null, null,
                Now));
    }

    [Theory]
    [InlineData("", "valid@example.com")]
    [InlineData("   ", "valid@example.com")]
    [InlineData("Valid Name", "")]
    [InlineData("Valid Name", "   ")]
    public void Create_WithInvalidRequiredFields_Throws(
        string fullName,
        string email)
    {
        Assert.Throws<ArgumentException>(
            () => CitizenProfile.Create(
                UserId,
                fullName,
                email,
                null, null, null, null, null, null, null,
                Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(151)]
    public void Create_WithOutOfRangeAge_Throws(int age)
    {
        Assert.Throws<ArgumentException>(
            () => CitizenProfile.Create(
                UserId,
                "Valid Name",
                "valid@example.com",
                null, null, null, age, null, null, null,
                Now));
    }

    [Fact]
    public void Create_WithOverlongFullName_Throws()
    {
        var tooLong = new string('a', 151);

        Assert.Throws<ArgumentException>(
            () => CitizenProfile.Create(
                UserId,
                tooLong,
                "valid@example.com",
                null, null, null, null, null, null, null,
                Now));
    }

    [Fact]
    public void Update_ChangesEditableFieldsAndTimestamp()
    {
        var profile = CitizenProfile.Create(
            UserId,
            "Valid Name",
            "valid@example.com",
            null, null, null, null, null, null, null,
            Now);

        var changedAt = Now.AddHours(1);

        profile.Update(
            " 9811111111  ",
            "  987654321  ",
            "  99-88-7777  ",
            41,
            "  Female  ",
            "  Pokhara  ",
            "  Butwal  ",
            changedAt);

        Assert.Equal("9811111111", profile.PhoneNumber);
        Assert.Equal("987654321", profile.NIDNumber);
        Assert.Equal("99-88-7777", profile.CitizenshipNumber);
        Assert.Equal(41, profile.Age);
        Assert.Equal("Female", profile.Gender);
        Assert.Equal("Pokhara", profile.PermanentAddress);
        Assert.Equal("Butwal", profile.TemporaryAddress);
        Assert.Equal(Now, profile.CreatedAt);
        Assert.Equal(changedAt, profile.UpdatedAt);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(200)]
    public void Update_WithOutOfRangeAge_Throws(int age)
    {
        var profile = CitizenProfile.Create(
            UserId,
            "Valid Name",
            "valid@example.com",
            null, null, null, null, null, null, null,
            Now);

        Assert.Throws<ArgumentException>(
            () => profile.Update(
                null, null, null, age, null, null, null, Now));
    }
}
