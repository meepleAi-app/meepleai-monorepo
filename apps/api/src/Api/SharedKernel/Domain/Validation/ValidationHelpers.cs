using Api.SharedKernel.Domain.Results;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.SharedKernel.Domain.Validation;

/// <summary>
/// Helper methods for working with validation results.
/// </summary>
public static class ValidationHelpers
{
    /// <summary>
    /// Throws a ValidationException if the result is a failure.
    /// Useful for fail-fast validation in constructors and Value Objects.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="result">The validation result.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ValidationException">Thrown if validation fails.</exception>
    public static T ThrowIfFailure<T>(this Result<T> result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (result.IsFailure)
        {
            throw new ValidationException(result.Error!.Message);
        }

        return result.Value!;
    }

    /// <summary>
    /// Throws a ValidationException with a specific field name if the result is a failure.
    /// Useful for fail-fast validation in constructors and Value Objects.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="result">The validation result.</param>
    /// <param name="fieldName">The name of the field being validated.</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ValidationException">Thrown if validation fails.</exception>
    public static T ThrowIfFailure<T>(this Result<T> result, string fieldName)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (fieldName is null) throw new ArgumentNullException(nameof(fieldName));
        if (result.IsFailure)
        {
            throw new ValidationException(fieldName, result.Error!.Message);
        }

        return result.Value!;
    }

    /// <summary>
    /// Converts multiple validation results into a single result.
    /// All validations must succeed for the combined result to succeed.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="results">The validation results to combine.</param>
    /// <returns>A combined result containing all errors if any validation fails.</returns>
    public static Result<T> CombineResults<T>(params Result<T>[] results)
    {
        if (results is null) throw new ArgumentNullException(nameof(results));
        if (results.Length == 0)
            throw new ArgumentException("At least one result is required", nameof(results));

        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Any())
        {
            var combinedMessage = string.Join("; ", failures.Select(f => f.Error!.Message));
            return Result<T>.Failure(Error.Validation(combinedMessage));
        }

        return results[0];
    }

    /// <summary>
    /// Executes multiple validations and returns the first failure, or the last success.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="value">The value to validate.</param>
    /// <param name="validators">The validation functions to apply in sequence.</param>
    /// <returns>The first failure result, or the final success result.</returns>
    public static Result<T> Validate<T>(T value, params Func<T, Result<T>>[] validators)
    {
        if (validators is null) throw new ArgumentNullException(nameof(validators));
        var result = Result<T>.Success(value);

        foreach (var validator in validators)
        {
            result = result.Then(validator);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a validation function from a predicate.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="predicate">The validation predicate.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>A validation function that returns a Result.</returns>
    public static Func<T, Result<T>> CreateValidator<T>(
        Func<T, bool> predicate,
        string errorMessage)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (errorMessage is null) throw new ArgumentNullException(nameof(errorMessage));
        return value => value.Must(predicate, errorMessage);
    }

    /// <summary>
    /// Creates a validation function from an async predicate.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="predicate">The async validation predicate.</param>
    /// <param name="errorMessage">The error message if validation fails.</param>
    /// <returns>An async validation function that returns a Result.</returns>
    public static Func<T, Task<Result<T>>> CreateAsyncValidator<T>(
        Func<T, Task<bool>> predicate,
        string errorMessage)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (errorMessage is null) throw new ArgumentNullException(nameof(errorMessage));
        return async value =>
        {
            var isValid = await predicate(value).ConfigureAwait(false);
            return isValid
                ? Result<T>.Success(value)
                : Result<T>.Failure(Error.Validation(errorMessage));
        };
    }
}
