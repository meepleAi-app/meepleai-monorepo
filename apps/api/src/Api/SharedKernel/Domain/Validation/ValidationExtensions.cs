using Api.SharedKernel.Domain.Results;

namespace Api.SharedKernel.Domain.Validation;

/// <summary>
/// Extension methods for common validation patterns.
/// Provides a fluent API for building validation chains that return Result{T} values.
/// </summary>
internal static class ValidationExtensions
{
    /// <summary>
    /// Validates that a string is not null, empty, or whitespace.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated string or an error.</returns>
    public static Result<string> NotNullOrWhiteSpace(
        this string? value,
        string parameterName,
        string? message = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be null, empty, or whitespace"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a string is not null or empty.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated string or an error.</returns>
    public static Result<string> NotNullOrEmpty(
        this string? value,
        string parameterName,
        string? message = null)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be null or empty"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a nullable GUID is not null or empty.
    /// </summary>
    /// <param name="value">The nullable GUID value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated GUID or an error.</returns>
    public static Result<Guid> NotNullOrEmpty(
        this Guid? value,
        string parameterName,
        string? message = null)
    {
        if (value is null || value == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be null or empty"));
        }

        return Result<Guid>.Success(value.Value);
    }

    /// <summary>
    /// Validates that a collection is not null or empty.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="value">The collection to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated collection or an error.</returns>
    public static Result<IEnumerable<T>> NotNullOrEmpty<T>(
        this IEnumerable<T>? value,
        string parameterName,
        string? message = null)
    {
        if (value is null || !value.Any())
        {
            return Result<IEnumerable<T>>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be null or empty"));
        }

        return Result<IEnumerable<T>>.Success(value);
    }

    /// <summary>
    /// Validates that a string has a minimum length.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="minLength">The minimum required length.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated string or an error.</returns>
    public static Result<string> MinLength(
        this string value,
        int minLength,
        string parameterName,
        string? message = null)
    {
        if (value.Length < minLength)
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must be at least {minLength} characters long"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a string has a maximum length.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated string or an error.</returns>
    public static Result<string> MaxLength(
        this string value,
        int maxLength,
        string parameterName,
        string? message = null)
    {
        if (value.Length > maxLength)
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} must not exceed {maxLength} characters"));
        }

        return Result<string>.Success(value);
    }

    /// <summary>
    /// Validates that a string matches a specific pattern.
    /// </summary>
    /// <param name="value">The string value to validate.</param>
    /// <param name="pattern">The regex pattern to match.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated string or an error.</returns>
    public static Result<string> MatchesPattern(
        this string value,
        string pattern,
        string parameterName,
        string? message = null)
    {
        // FIX MA0009: Add timeout to prevent ReDoS attacks
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, pattern, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return Result<string>.Failure(Error.Validation(
                message ?? $"{parameterName} does not match the required pattern"));
        }

        return Result<string>.Success(value);
    }
    /// <summary>
    /// Validates that a GUID is not empty.
    /// </summary>
    /// <param name="value">The GUID value to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated GUID or an error.</returns>
    public static Result<Guid> NotEmpty(
        this Guid value,
        string parameterName,
        string? message = null)
    {
        if (value == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be empty"));
        }

        return Result<Guid>.Success(value);
    }

    /// <summary>
    /// Validates that a number is greater than a minimum value.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="minValue">The minimum allowed value (exclusive).</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> GreaterThan<T>(
        this T value,
        T minValue,
        string parameterName,
        string? message = null) where T : IComparable<T>
    {
        if (value.CompareTo(minValue) <= 0)
        {
            return Result<T>.Failure(Error.Validation(
                message ?? $"{parameterName} must be greater than {minValue}"));
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates that a number is greater than or equal to a minimum value.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="minValue">The minimum allowed value (inclusive).</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> GreaterThanOrEqual<T>(
        this T value,
        T minValue,
        string parameterName,
        string? message = null) where T : IComparable<T>
    {
        if (value.CompareTo(minValue) < 0)
        {
            return Result<T>.Failure(Error.Validation(
                message ?? $"{parameterName} must be greater than or equal to {minValue}"));
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates that a number is less than a maximum value.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="maxValue">The maximum allowed value (exclusive).</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> LessThan<T>(
        this T value,
        T maxValue,
        string parameterName,
        string? message = null) where T : IComparable<T>
    {
        if (value.CompareTo(maxValue) >= 0)
        {
            return Result<T>.Failure(Error.Validation(
                message ?? $"{parameterName} must be less than {maxValue}"));
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates that a number is less than or equal to a maximum value.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="maxValue">The maximum allowed value (inclusive).</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> LessThanOrEqual<T>(
        this T value,
        T maxValue,
        string parameterName,
        string? message = null) where T : IComparable<T>
    {
        if (value.CompareTo(maxValue) > 0)
        {
            return Result<T>.Failure(Error.Validation(
                message ?? $"{parameterName} must be less than or equal to {maxValue}"));
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates that a number is within a specific range.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="minValue">The minimum allowed value (inclusive).</param>
    /// <param name="maxValue">The maximum allowed value (inclusive).</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> InRange<T>(
        this T value,
        T minValue,
        T maxValue,
        string parameterName,
        string? message = null) where T : IComparable<T>
    {
        if (value.CompareTo(minValue) < 0 || value.CompareTo(maxValue) > 0)
        {
            return Result<T>.Failure(Error.Validation(
                message ?? $"{parameterName} must be between {minValue} and {maxValue}"));
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates that a collection has a specific count.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="value">The collection to validate.</param>
    /// <param name="expectedCount">The expected count.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated collection or an error.</returns>
    public static Result<IEnumerable<T>> HasCount<T>(
        this IEnumerable<T> value,
        int expectedCount,
        string parameterName,
        string? message = null)
    {
        var count = value.Count();
        if (count != expectedCount)
        {
            return Result<IEnumerable<T>>.Failure(Error.Validation(
                message ?? $"{parameterName} must have exactly {expectedCount} items, but has {count}"));
        }

        return Result<IEnumerable<T>>.Success(value);
    }

    /// <summary>
    /// Validates that a collection has a minimum count.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="value">The collection to validate.</param>
    /// <param name="minCount">The minimum required count.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated collection or an error.</returns>
    public static Result<IEnumerable<T>> HasMinCount<T>(
        this IEnumerable<T> value,
        int minCount,
        string parameterName,
        string? message = null)
    {
        var count = value.Count();
        if (count < minCount)
        {
            return Result<IEnumerable<T>>.Failure(Error.Validation(
                message ?? $"{parameterName} must have at least {minCount} items, but has {count}"));
        }

        return Result<IEnumerable<T>>.Success(value);
    }
    /// <summary>
    /// Validates that an object is not null.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="value">The object to validate.</param>
    /// <param name="parameterName">The name of the parameter being validated.</param>
    /// <param name="message">Optional custom error message.</param>
    /// <returns>A Result containing the validated object or an error.</returns>
    public static Result<T> NotNull<T>(
        this T? value,
        string parameterName,
        string? message = null) where T : class
    {
        if (value is null)
        {
            return Result<T>.Failure(Error.Validation(
                message ?? $"{parameterName} cannot be null"));
        }

        return Result<T>.Success(value);
    }
    /// <summary>
    /// Allows chaining additional validation on a successful result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The current result.</param>
    /// <param name="validator">The next validation function to apply.</param>
    /// <returns>The result of the next validation, or the current failure.</returns>
    public static Result<T> Then<T>(
        this Result<T> result,
        Func<T, Result<T>> validator)
    {
        if (result.IsFailure)
        {
            return result;
        }

        return validator(result.Value!);
    }

    /// <summary>
    /// Validates using a custom predicate.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="predicate">The validation predicate.</param>
    /// <param name="error">The error to return if validation fails.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> Must<T>(
        this T value,
        Func<T, bool> predicate,
        Error error)
    {
        if (!predicate(value))
        {
            return Result<T>.Failure(error);
        }

        return Result<T>.Success(value);
    }

    /// <summary>
    /// Validates using a custom predicate with a simple error message.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="predicate">The validation predicate.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>A Result containing the validated value or an error.</returns>
    public static Result<T> Must<T>(
        this T value,
        Func<T, bool> predicate,
        string errorMessage)
    {
        return value.Must(predicate, Error.Validation(errorMessage));
    }
}
