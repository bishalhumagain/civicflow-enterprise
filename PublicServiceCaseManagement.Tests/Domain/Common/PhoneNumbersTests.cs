using PublicServiceCaseManagement.Api.Domain.Common;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Domain;

public sealed class PhoneNumbersTests
{
    [Fact]
    public void Normalize_WithBlankValue_ReturnsNull()
    {
        Assert.Null(PhoneNumbers.Normalize(null));
        Assert.Null(PhoneNumbers.Normalize("   "));
    }

    [Fact]
    public void Normalize_TrimsTheValue()
    {
        var result = PhoneNumbers.Normalize("  +977-9800000000  ");

        Assert.Equal("+977-9800000000", result);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12abc34")]
    [InlineData("12345")]
    [InlineData("+977-980000000000000000000000000000000")]
    public void Normalize_WithInvalidValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => PhoneNumbers.Normalize(value));
    }

    [Fact]
    public void Normalize_WithValidValue_ReturnsTheValue()
    {
        var result = PhoneNumbers.Normalize("(980) 000-0000");

        Assert.Equal("(980) 000-0000", result);
    }
}
