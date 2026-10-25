namespace PublicServiceCaseManagement.Api.Domain.Common;

/// <summary>
/// Shared validation for the optional contact phone number that is collected
/// for citizens, staff members, and invitations.
/// </summary>
public static class PhoneNumbers
{
    public const int MaximumLength = 30;

    public const int MinimumLength = 6;

    /// <summary>
    /// Normalizes a phone number, or returns <c>null</c> when none was
    /// supplied. Invalid values are rejected instead of being silently
    /// dropped so the caller gets immediate feedback.
    /// </summary>
    public static string? Normalize(
        string? value,
        string parameterName = "phoneNumber")
    {
        if (TryNormalize(value, out var normalized, out var error))
        {
            return normalized;
        }

        throw new ArgumentException(
            error ?? "The phone number is invalid.",
            parameterName);
    }

    /// <summary>
    /// Attempts to normalize a phone number. Returns <c>true</c> for a null or
    /// whitespace-only value with <paramref name="normalized"/> set to
    /// <c>null</c>; returns <c>false</c> with a human-readable
    /// <paramref name="error"/> when the value is not acceptable.
    /// </summary>
    public static bool TryNormalize(
        string? value,
        out string? normalized,
        out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();

        if (trimmed.Length < MinimumLength ||
            trimmed.Length > MaximumLength)
        {
            error = "A phone number must be between " + MinimumLength +
                " and " + MaximumLength + " characters.";
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!IsAllowed(character))
            {
                error = "A phone number may only contain digits, spaces, and " +
                    "the characters + - ( ) .";
                return false;
            }
        }

        if (trimmed.Count(char.IsDigit) < MinimumLength)
        {
            error = "A phone number must contain at least " +
                MinimumLength + " digits.";
            return false;
        }

        normalized = trimmed;
        return true;
    }

    private static bool IsAllowed(char character) =>
        char.IsDigit(character) ||
        character == (char)32 ||
        character == (char)43 ||
        character == (char)45 ||
        character == (char)40 ||
        character == (char)41 ||
        character == (char)46 ||
        character == (char)47;
}
