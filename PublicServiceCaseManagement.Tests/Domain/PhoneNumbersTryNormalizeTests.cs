using PublicServiceCaseManagement.Api.Domain;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Domain;

public sealed class PhoneNumbersTryNormalizeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_WithNoValue_SucceedsWithNull(string? value)
    {
        var succeeded = PhoneNumbers.TryNormalize(
            value,
            out var normalized,
            out var error);

        Assert.True(succeeded);
        Assert.Null(normalized);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalize_TrimsTheValue()
    {
        var succeeded = PhoneNumbers.TryNormalize(
            "  +977-9800000000  ",
            out var normalized,
            out var error);

        Assert.True(succeeded);
        Assert.Equal("+977-9800000000", normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("(980) 000-0000")]
    [InlineData("+977.980.000.0000")]
    [InlineData("+977/9800000000")]
    [InlineData("980 000 0000")]
    public void TryNormalize_WithSupportedCharacters_Succeeds(string value)
    {
        var succeeded = PhoneNumbers.TryNormalize(
            value,
            out var normalized,
            out var error);

        Assert.True(succeeded);
        Assert.Equal(value, normalized);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("+977-980000000000000000000000000000000")]
    public void TryNormalize_WithInvalidLength_Fails(string value)
    {
        var succeeded = PhoneNumbers.TryNormalize(
            value,
            out var normalized,
            out var error);

        Assert.False(succeeded);
        Assert.Null(normalized);
        Assert.Contains("between 6 and 30 characters", error);
    }

    [Theory]
    [InlineData("98000O0000")]
    [InlineData("98000+0000x")]
    [InlineData("98000#0000")]
    [InlineData("abc123456")]
    public void TryNormalize_WithUnsupportedCharacters_Fails(string value)
    {
        var succeeded = PhoneNumbers.TryNormalize(
            value,
            out var normalized,
            out var error);

        Assert.False(succeeded);
        Assert.Null(normalized);
        Assert.Contains("may only contain digits", error);
    }

    [Theory]
    [InlineData("12-34-5")]
    [InlineData("(98)-00")]
    public void TryNormalize_WithTooFewDigits_Fails(string value)
    {
        var succeeded = PhoneNumbers.TryNormalize(
            value,
            out var normalized,
            out var error);

        Assert.False(succeeded);
        Assert.Null(normalized);
        Assert.Contains("at least 6 digits", error);
    }

    [Fact]
    public void Normalize_WithInvalidValue_ThrowsWithParameterName()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => PhoneNumbers.Normalize("abc", "PhoneNumber"));

        Assert.Equal("PhoneNumber", exception.ParamName);
        Assert.Contains("phone number", exception.Message);
    }
}
