using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Validation;
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
        var validatedEmail = value
            .NotNullOrWhiteSpace(nameof(Email), "Email cannot be empty")
            .Then(e => e.Trim().MaxLength(256, nameof(Email), "Email cannot exceed 256 characters"))
            .Then(e => e.Must(
                email => EmailRegex.IsMatch(email),
                $"Invalid email format: {value}"))
            .ThrowIfFailure(nameof(Email));

        // Normalize to lowercase for consistency
        Value = validatedEmail.ToLowerInvariant();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;

    public static Email Parse(string value) => new(value);
}
