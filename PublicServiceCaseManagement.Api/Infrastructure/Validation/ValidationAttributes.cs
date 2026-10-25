using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain;

namespace PublicServiceCaseManagement.Api.Infrastructure.Validation;

/// <summary>
/// Rejects strings that are null, empty, or made up only of whitespace.
/// Combine with <see cref="RequiredAttribute"/> for non-nullable contracts.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public NotWhiteSpaceAttribute()
        : base("The {0} field must not be empty or whitespace.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not string text || !string.IsNullOrWhiteSpace(text);
}

/// <summary>
/// Validates a string after trimming surrounding whitespace, so padding can
/// never be used to satisfy a minimum length or to exceed a maximum length.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false)]
public sealed class TrimmedStringLengthAttribute(int maximumLength)
    : ValidationAttribute
{
    public int MinimumLength { get; set; }

    public int MaximumLength { get; } = maximumLength;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text)
        {
            return false;
        }

        var length = text.Trim().Length;

        return length >= MinimumLength && length <= MaximumLength;
    }

    public override string FormatErrorMessage(string name) =>
        MinimumLength > 0
            ? name + " must be between " + MinimumLength + " and " + MaximumLength + " characters."
            : name + " must not exceed " + MaximumLength + " characters.";
}

/// <summary>
/// Validates the same phone-number shape the domain uses for storage. A null
/// value is valid so the attribute can be combined with optional fields.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false)]
public sealed class PhoneNumberAttribute : ValidationAttribute
{
    public PhoneNumberAttribute()
        : base(
            "Enter a valid phone number (" + PhoneNumbers.MinimumLength +
            "-" + PhoneNumbers.MaximumLength + " characters, at least " +
            PhoneNumbers.MinimumLength + " digits).")
    {
    }

    public override bool IsValid(object? value) =>
        value is null ||
        (value is string text &&
         PhoneNumbers.TryNormalize(text, out _, out _));
}

/// <summary>
/// Restricts a string field to a fixed set of values. Comparison is
/// case-insensitive by default so enum-like payloads remain tolerant.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false)]
public sealed class AllowedValuesAttribute(params string[] allowedValues)
    : ValidationAttribute
{
    private readonly string[] _allowedValues = allowedValues;

    public bool IgnoreCase { get; set; } = true;

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string text)
        {
            return false;
        }

        var comparison = IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return _allowedValues.Any(candidate =>
            string.Equals(candidate, text.Trim(), comparison));
    }

    public override string FormatErrorMessage(string name) =>
        name + " must be one of: " + string.Join(", ", _allowedValues) + ".";
}

/// <summary>
/// Rejects the default value of a required identifier.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter,
    AllowMultiple = false)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute()
        : base("The {0} field must be a non-empty identifier.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not Guid guid || guid != Guid.Empty;
}
