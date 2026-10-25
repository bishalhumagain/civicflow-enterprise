using PublicServiceCaseManagement.Api.Infrastructure.Validation;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Infrastructure;

public sealed class ValidationAttributesTests
{
    [Fact]
    public void NotWhiteSpace_RejectsEmptyAndWhitespace()
    {
        var attribute = new NotWhiteSpaceAttribute();

        Assert.False(attribute.IsValid(string.Empty));
        Assert.False(attribute.IsValid("   "));
        Assert.False(attribute.IsValid("\t\r\n"));
    }

    [Fact]
    public void NotWhiteSpace_AcceptsText()
    {
        var attribute = new NotWhiteSpaceAttribute();

        Assert.True(attribute.IsValid("Bishal Humagain"));
        Assert.True(attribute.IsValid("  Bishal  "));
    }

    [Fact]
    public void TrimmedStringLength_MeasuresTrimmedValue()
    {
        var attribute = new TrimmedStringLengthAttribute(5)
        {
            MinimumLength = 3
        };

        Assert.True(attribute.IsValid("  abc  "));
        Assert.False(attribute.IsValid("  ab  "));
        Assert.False(attribute.IsValid("  abcdef  "));
    }

    [Fact]
    public void TrimmedStringLength_AllowsNullForOptionalFields()
    {
        var attribute = new TrimmedStringLengthAttribute(5);

        Assert.True(attribute.IsValid(null));
    }

    [Fact]
    public void TrimmedStringLength_RejectsNonStringValues()
    {
        var attribute = new TrimmedStringLengthAttribute(5);

        Assert.False(attribute.IsValid(42));
    }

    [Fact]
    public void TrimmedStringLength_FormatsRangeMessage()
    {
        var attribute = new TrimmedStringLengthAttribute(5)
        {
            MinimumLength = 3
        };

        Assert.Equal(
            "Title must be between 3 and 5 characters.",
            attribute.FormatErrorMessage("Title"));
    }

    [Fact]
    public void TrimmedStringLength_FormatsMaximumMessage()
    {
        var attribute = new TrimmedStringLengthAttribute(5);

        Assert.Equal(
            "Title must not exceed 5 characters.",
            attribute.FormatErrorMessage("Title"));
    }

    [Fact]
    public void PhoneNumber_AllowsNullAndBlank()
    {
        var attribute = new PhoneNumberAttribute();

        Assert.True(attribute.IsValid(null));
        Assert.True(attribute.IsValid("   "));
    }

    [Theory]
    [InlineData("(980) 000-0000")]
    [InlineData("+977-9800000000")]
    public void PhoneNumber_AcceptsSupportedValues(string value)
    {
        var attribute = new PhoneNumberAttribute();

        Assert.True(attribute.IsValid(value));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("98000O0000")]
    public void PhoneNumber_RejectsInvalidValues(string value)
    {
        var attribute = new PhoneNumberAttribute();

        Assert.False(attribute.IsValid(value));
    }

    [Fact]
    public void AllowedValues_IsCaseInsensitiveAndTrims()
    {
        var attribute = new AllowedValuesAttribute("Citizen", "Staff");

        Assert.True(attribute.IsValid("  citizen  "));
        Assert.True(attribute.IsValid("STAFF"));
        Assert.False(attribute.IsValid("SuperAdmin"));
    }

    [Fact]
    public void AllowedValues_CanBeCaseSensitive()
    {
        var attribute = new AllowedValuesAttribute("Citizen")
        {
            IgnoreCase = false
        };

        Assert.True(attribute.IsValid("Citizen"));
        Assert.False(attribute.IsValid("citizen"));
    }

    [Fact]
    public void AllowedValues_AllowsNull()
    {
        var attribute = new AllowedValuesAttribute("Citizen");

        Assert.True(attribute.IsValid(null));
    }

    [Fact]
    public void AllowedValues_FormatsAvailableOptions()
    {
        var attribute = new AllowedValuesAttribute("Citizen", "Staff");

        Assert.Equal(
            "Role must be one of: Citizen, Staff.",
            attribute.FormatErrorMessage("Role"));
    }

    [Fact]
    public void NotEmptyGuid_RejectsDefaultValue()
    {
        var attribute = new NotEmptyGuidAttribute();

        Assert.False(attribute.IsValid(Guid.Empty));
        Assert.True(attribute.IsValid(Guid.NewGuid()));
        Assert.True(attribute.IsValid(null));
    }
}
