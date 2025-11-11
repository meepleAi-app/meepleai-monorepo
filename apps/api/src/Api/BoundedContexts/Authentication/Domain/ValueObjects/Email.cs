using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Represents an email address value object.
/// Ensures email validity and normalization.
/// </summary>
public sealed class Email : ValueObject
{
    // Email validation regex (RFC 5322 simplified)
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(nameof(Email), "Email cannot be empty");

        if (value.Length > 256)
            throw new ValidationException(nameof(Email), "Email cannot exceed 256 characters");

        var trimmedValue = value.Trim();
        if (!EmailRegex.IsMatch(trimmedValue))
            throw new ValidationException(nameof(Email), $"Invalid email format: {value}");

        // Normalize to lowercase for consistency
        Value = trimmedValue.ToLowerInvariant();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;

    public static Email Parse(string value) => new(value);
}
