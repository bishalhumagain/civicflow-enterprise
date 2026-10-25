namespace PublicServiceCaseManagement.Api.Domain.Common;

/// <summary>
/// Shared, deliberately conservative normalization rules for user-supplied
/// text. Normalization only removes accidental noise (outer whitespace,
/// repeated spaces) and never rewrites meaningful characters, casing of
/// display text, or Unicode content.
/// </summary>
public static class TextNormalization
{
    public const int MaximumNameLength = 150;

    /// <summary>
    /// Collapses every run of whitespace into a single space and trims the
    /// result. Unicode letters and punctuation are preserved verbatim.
    /// </summary>
    public static string CollapseWhitespace(string? value) =>
        string.Join(
            (char)32,
            (value ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Normalizes a display name and validates its bounds.
    /// </summary>
    public static string NormalizeName(
        string? value,
        string parameterName = "fullName")
    {
        var collapsed = CollapseWhitespace(value);

        if (collapsed.Length == 0)
        {
            throw new ArgumentException(
                "Full name is required.",
                parameterName);
        }

        if (collapsed.Length > MaximumNameLength)
        {
            throw new ArgumentException(
                "Full name is too long.",
                parameterName);
        }

        return collapsed;
    }

    /// <summary>
    /// Trims an optional value and maps an empty or whitespace-only value to
    /// <c>null</c>. Empty strings are never persisted for optional fields.
    /// </summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Trims and upper-cases an optional identifier (for example an NID or
    /// citizenship number) so uniqueness checks are consistent.
    /// </summary>
    public static string? NormalizeOptionalIdentifier(string? value) =>
        NormalizeOptional(value)?.ToUpperInvariant();

    /// <summary>
    /// Trims and lower-cases an email address. Validation still happens in the
    /// DTO and domain layers; this only produces the canonical storage form.
    /// </summary>
    public static string NormalizeEmail(
        string? value,
        string parameterName = "email")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email is required.", parameterName);
        }

        return value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Trims free text while preserving internal whitespace and line breaks.
    /// </summary>
    public static string NormalizeMultiline(string? value) =>
        (value ?? string.Empty).Trim();

    /// <summary>
    /// Detects embedded control characters that should never be stored in
    /// single-line name or identifier fields.
    /// </summary>
    public static bool HasControlCharacters(string? value) =>
        value is not null &&
        value.Any(character =>
            character is not (char)9 and not (char)13 and not (char)10 &&
            char.IsControl(character));
}
