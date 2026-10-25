using PublicServiceCaseManagement.Api.Domain.Common;
using Xunit;

namespace PublicServiceCaseManagement.Tests.Domain;

public sealed class TextNormalizationTests
{
    [Fact]
    public void CollapseWhitespace_TrimsAndCollapsesRuns()
    {
        var result = TextNormalization.CollapseWhitespace(
            "  Bishal\t\tHumagain \n");

        Assert.Equal("Bishal Humagain", result);
    }

    [Fact]
    public void CollapseWhitespace_WithNullOrBlank_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, TextNormalization.CollapseWhitespace(null));
        Assert.Equal(
            string.Empty,
            TextNormalization.CollapseWhitespace(" \t\r\n "));
    }

    [Fact]
    public void CollapseWhitespace_PreservesUnicodeAndPunctuation()
    {
        var result = TextNormalization.CollapseWhitespace(
            "  सुनिता   श्रेष्ठ  ");

        Assert.Equal("सुनिता श्रेष्ठ", result);
    }

    [Fact]
    public void NormalizeName_WithPadding_ReturnsCanonicalName()
    {
        var result = TextNormalization.NormalizeName(
            "  José   María  ");

        Assert.Equal("José María", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void NormalizeName_WithMissingValue_Throws(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TextNormalization.NormalizeName(value));

        Assert.Contains("Full name is required.", exception.Message);
    }

    [Fact]
    public void NormalizeName_AtMaximumLength_IsAccepted()
    {
        var name = new string((char)65, TextNormalization.MaximumNameLength);

        var result = TextNormalization.NormalizeName(name);

        Assert.Equal(name, result);
    }

    [Fact]
    public void NormalizeName_MeasuresLengthAfterTrimming()
    {
        var name = new string((char)65, TextNormalization.MaximumNameLength);

        var result = TextNormalization.NormalizeName("  " + name + "  ");

        Assert.Equal(name, result);
    }

    [Theory]
    [InlineData(151)]
    [InlineData(500)]
    public void NormalizeName_OverMaximumLength_Throws(int length)
    {
        var name = new string((char)65, length);

        var exception = Assert.Throws<ArgumentException>(
            () => TextNormalization.NormalizeName(name));

        Assert.Contains("Full name is too long.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeOptional_WithBlankValue_ReturnsNull(string? value)
    {
        Assert.Null(TextNormalization.NormalizeOptional(value));
    }

    [Fact]
    public void NormalizeOptional_TrimsButPreservesInnerSpacing()
    {
        var result = TextNormalization.NormalizeOptional(
            "  Kathmandu  Valley  ");

        Assert.Equal("Kathmandu  Valley", result);
    }

    [Fact]
    public void NormalizeOptionalIdentifier_TrimsAndUppercases()
    {
        var result = TextNormalization.NormalizeOptionalIdentifier(
            "  nid-123-a  ");

        Assert.Equal("NID-123-A", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeOptionalIdentifier_WithBlankValue_ReturnsNull(
        string? value)
    {
        Assert.Null(TextNormalization.NormalizeOptionalIdentifier(value));
    }

    [Fact]
    public void NormalizeEmail_TrimsAndLowercases()
    {
        var result = TextNormalization.NormalizeEmail(
            "  Citizen.One@Example.TEST  ");

        Assert.Equal("citizen.one@example.test", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeEmail_WithMissingValue_Throws(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => TextNormalization.NormalizeEmail(value));

        Assert.Contains("Email is required.", exception.Message);
    }

    [Fact]
    public void NormalizeMultiline_TrimsButPreservesLineBreaks()
    {
        var result = TextNormalization.NormalizeMultiline(
            "\n  First line.\n\n  Second line.  \n");

        Assert.Equal("First line.\n\n  Second line.", result);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Bishal", false)]
    [InlineData("Line\tTab", false)]
    [InlineData("Line\nBreak", false)]
    [InlineData("Line\rBreak", false)]
    [InlineData("Bad\u0000Value", true)]
    [InlineData("Bad\u001FValue", true)]
    [InlineData("Bad\u007FValue", true)]
    public void HasControlCharacters_DetectsEmbeddedControls(
        string? value,
        bool expected)
    {
        Assert.Equal(expected, TextNormalization.HasControlCharacters(value));
    }
}
