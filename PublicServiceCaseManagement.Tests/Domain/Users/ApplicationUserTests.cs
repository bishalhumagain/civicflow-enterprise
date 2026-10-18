using PublicServiceCaseManagement.Api.Domain.Users;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Domain.Users;

public sealed class ApplicationUserTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsNameAndNormalizesEmail()
    {
        var user = ApplicationUser.Create(
            "  Bishal Humagain  ",
            "  Bishal@example.com  ",
            Now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Bishal Humagain", user.FullName);
        Assert.Equal("Bishal@example.com", user.Email);
        Assert.Equal("BISHAL@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal(PlatformRole.None, user.PlatformRole);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Theory]
    [InlineData("", "valid@example.com")]
    [InlineData("   ", "valid@example.com")]
    [InlineData("Valid Name", "invalid-email")]
    [InlineData("Valid Name", "")]
    public void Create_WithInvalidInput_ThrowsArgumentException(
        string fullName,
        string email)
    {
        Assert.Throws<ArgumentException>(
            () => ApplicationUser.Create(fullName, email, Now));
    }

    [Fact]
    public void SetPlatformRole_UpdatesRoleAndTimestamp()
    {
        var user = ApplicationUser.Create(
            "Platform Administrator",
            "admin@example.com",
            Now);

        var changedAt = Now.AddMinutes(5);

        user.SetPlatformRole(PlatformRole.SuperAdmin, changedAt);

        Assert.Equal(PlatformRole.SuperAdmin, user.PlatformRole);
        Assert.Equal(changedAt, user.UpdatedAt);
    }

    [Fact]
    public void SetPlatformRole_WithSameRole_DoesNotChangeTimestamp()
    {
        var user = ApplicationUser.Create(
            "Regular User",
            "user@example.com",
            Now);

        user.SetPlatformRole(PlatformRole.None, Now.AddMinutes(5));

        Assert.Equal(Now, user.UpdatedAt);
    }

    [Fact]
    public void SetPlatformRole_WithInvalidRole_Throws()
    {
        var user = ApplicationUser.Create(
            "Regular User",
            "user@example.com",
            Now);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => user.SetPlatformRole((PlatformRole)999, Now));
    }
}
