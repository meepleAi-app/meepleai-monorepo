using Api.SharedKernel.Domain.Exceptions;

namespace Api.SharedKernel.Guards;

/// <summary>
/// Guard clauses for fail-fast validation in Value Objects and Domain Entities.
/// Throws exceptions immediately on validation failure (fail-fast pattern).
/// For Result-based validation flows, use ValidationExtensions instead.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Ensures value is not negative (domain business rule).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <exception cref="ValidationException">Value is negative</exception>
    public static void AgainstNegative(decimal value, string paramName)
    {
        if (value < 0)
            throw new ValidationException($"{paramName} cannot be negative");
    }

    /// <summary>
    /// Ensures integer value is not negative (domain business rule).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <exception cref="ValidationException">Value is negative</exception>
    public static void AgainstNegative(int value, string paramName)
    {
        if (value < 0)
            throw new ValidationException($"{paramName} cannot be negative");
    }

    /// <summary>
    /// Ensures long value is not negative (domain business rule).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <exception cref="ValidationException">Value is negative</exception>
    public static void AgainstNegative(long value, string paramName)
    {
        if (value < 0)
            throw new ValidationException($"{paramName} cannot be negative");
    }

    /// <summary>
    /// Ensures value is within specified range (inclusive, domain business rule).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <param name="min">Minimum allowed value (inclusive)</param>
    /// <param name="max">Maximum allowed value (inclusive)</param>
    /// <exception cref="ValidationException">Value outside range</exception>
    public static void AgainstOutOfRange(decimal value, string paramName, decimal min, decimal max)
    {
        if (value < min || value > max)
            throw new ValidationException($"{paramName} must be between {min} and {max}");
    }

    /// <summary>
    /// Ensures integer value is within specified range (inclusive, domain business rule).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <param name="min">Minimum allowed value (inclusive)</param>
    /// <param name="max">Maximum allowed value (inclusive)</param>
    /// <exception cref="ValidationException">Value outside range</exception>
    public static void AgainstOutOfRange(int value, string paramName, int min, int max)
    {
        if (value < min || value > max)
            throw new ValidationException($"{paramName} must be between {min} and {max}");
    }

    /// <summary>
    /// Ensures string is not null, empty, or whitespace.
    /// </summary>
    /// <param name="value">String to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <exception cref="ArgumentException">String is null, empty, or whitespace</exception>
    public static void AgainstNullOrWhiteSpace(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} cannot be empty", paramName);
    }

    /// <summary>
    /// Ensures value is less than maximum (exclusive, domain business rule).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <param name="max">Maximum allowed value (exclusive)</param>
    /// <exception cref="ValidationException">Thrown when value is greater than or equal to max</exception>
    public static void AgainstTooLarge(int value, string paramName, int max)
    {
        if (value >= max)
            throw new ValidationException($"{paramName} must be less than {max}");
    }

    /// <summary>
    /// Ensures value is greater than minimum (exclusive).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <param name="min">Minimum allowed value (exclusive)</param>
    /// <exception cref="ValidationException">Thrown when value is less than or equal to min</exception>
    public static void AgainstTooSmall(int value, string paramName, int min)
    {
        if (value < min)
            throw new ValidationException($"{paramName} must be at least {min}");
    }

    /// <summary>
    /// Ensures value is greater than minimum (exclusive).
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="paramName">Parameter name for error message</param>
    /// <param name="min">Minimum allowed value (exclusive)</param>
    /// <exception cref="ValidationException">Thrown when value is less than or equal to min</exception>
    public static void AgainstTooSmall(long value, string paramName, long min)
    {
        if (value < min)
            throw new ValidationException($"{paramName} must be at least {min}");
    }

    /// <summary>
    /// Ensures first value does not exceed second value.
    /// </summary>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <param name="minParamName">Parameter name for minimum value</param>
    /// <param name="maxParamName">Parameter name for maximum value</param>
    /// <exception cref="ValidationException">Thrown when min exceeds max</exception>
    public static void AgainstInvalidRange(int min, int max, string minParamName, string maxParamName)
    {
        if (min > max)
            throw new ValidationException($"{minParamName} cannot exceed {maxParamName}");
    }
}
