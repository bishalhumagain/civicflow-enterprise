namespace PublicServiceCaseManagement.Api.Domain;

/// <summary>
/// Shared validation for the optional contact phone number that is collected
/// when an administrator onboards a staff member.
/// </summary>
public static class PhoneNumbers
{
    public const int MaximumLength = 30;

    public const int MinimumLength = 6;

    /// <summary>
    /// Normalizes a phone number, or returns <c>null</c> when none was
    /// supplied. Invalid values are rejected instead of being silently
    /// dropped so the administrator gets immediate feedback.
    /// </summary>
    public static string? Normalize(
        string? value,
        string parameterName = "phoneNumber")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length < MinimumLength ||
            trimmed.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"A phone number must be between {MinimumLength} and " +
                $"{MaximumLength} characters.",
                parameterName);
        }

        foreach (var character in trimmed)
        {
            if (!IsAllowed(character))
            {
                throw new ArgumentException(
                    "A phone number may only contain digits, spaces, and " +
                    "the characters + - ( ) .",
                    parameterName);
            }
        }

        var digitCount = trimmed.Count(char.IsDigit);

        if (digitCount < MinimumLength)
        {
            throw new ArgumentException(
                "A phone number must contain at least " +
                $"{MinimumLength} digits.",
                parameterName);
        }

        return trimmed;
    }

    private static bool IsAllowed(char character) =>
        char.IsDigit(character) ||
        character is ' ' or '+' or '-' or '(' or ')' or '.' or '/';
}
