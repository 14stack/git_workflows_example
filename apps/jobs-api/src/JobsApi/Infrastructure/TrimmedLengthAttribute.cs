using System.ComponentModel.DataAnnotations;

namespace JobsApi.Infrastructure;

/// <summary>
/// Like <see cref="StringLengthAttribute"/>, but measures the value after trimming, matching how the
/// domain stores it. A property-level attribute (rather than <see cref="IValidatableObject"/>) so the
/// error is reported together with every other field error.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TrimmedLengthAttribute(int minimumLength, int maximumLength) : ValidationAttribute
{
    public int MinimumLength { get; } = minimumLength;

    public int MaximumLength { get; } = maximumLength;

    public override bool IsValid(object? value) =>
        value is not string text || text.Trim().Length is var length && length >= MinimumLength && length <= MaximumLength;

    public override string FormatErrorMessage(string name) =>
        $"The {name} field must be between {MinimumLength} and {MaximumLength} characters, ignoring surrounding whitespace.";
}
