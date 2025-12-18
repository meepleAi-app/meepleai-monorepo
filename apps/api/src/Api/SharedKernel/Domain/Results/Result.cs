

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.SharedKernel.Domain.Results;

/// <summary>
/// Represents the result of an operation that may succeed or fail.
/// Encapsulates success/failure state with optional value and error information.
/// </summary>
/// <typeparam name="T">The type of the value in case of success.</typeparam>
internal sealed record Result<T>
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded, otherwise null.
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Gets the error information if the operation failed, otherwise null.
    /// </summary>
    public Error? Error { get; init; }

    private Result()
    {
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <param name="value">The result value.</param>
    /// <returns>A successful Result instance.</returns>
    public static Result<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value,
        Error = null
    };

    /// <summary>
    /// Creates a failure result with an error.
    /// </summary>
    /// <param name="error">The error information.</param>
    /// <returns>A failed Result instance.</returns>
    public static Result<T> Failure(Error error) => new()
    {
        IsSuccess = false,
        Value = default,
        Error = error
    };

    /// <summary>
    /// Creates a failure result from an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed Result instance.</returns>
    public static Result<T> Failure(Exception exception) => new()
    {
        IsSuccess = false,
        Value = default,
        Error = new Error(
            Code: exception.GetType().Name,
            Message: exception.Message,
            Details: exception.StackTrace
        )
    };

    /// <summary>
    /// Executes the appropriate action based on the result state.
    /// </summary>
    /// <param name="onSuccess">Action to execute if the operation succeeded.</param>
    /// <param name="onFailure">Action to execute if the operation failed.</param>
    public void Match(
        Action<T> onSuccess,
        Action<Error> onFailure)
    {
        if (IsSuccess && Value is not null)
        {
            onSuccess(Value);
        }
        else if (Error is not null)
        {
            onFailure(Error);
        }
    }

    /// <summary>
    /// Transforms the result to another type using the provided function.
    /// </summary>
    /// <typeparam name="TResult">The target type.</typeparam>
    /// <param name="transform">The transformation function.</param>
    /// <returns>A new Result with the transformed value or the same error.</returns>
    public Result<TResult> Map<TResult>(Func<T, TResult> transform)
    {
        if (IsSuccess && Value is not null)
        {
            return Result<TResult>.Success(transform(Value));
        }

        return Result<TResult>.Failure(Error ?? new Error(Code: "Unknown", Message: "No error information available"));
    }
}

/// <summary>
/// Represents error information for failed operations.
/// </summary>
/// <param name="Code">A machine-readable error code (e.g., "validation_error", "not_found").</param>
/// <param name="Message">A human-readable error message.</param>
/// <param name="Details">Optional additional error details (e.g., stack trace, validation errors).</param>
internal sealed record Error(
    string Code,
    string Message,
    string? Details = null
)
{
    /// <summary>
    /// Predefined error for validation failures.
    /// </summary>
    public static Error Validation(string message) => new("validation_error", message);

    /// <summary>
    /// Predefined error for not found scenarios.
    /// </summary>
    public static Error NotFound(string message = "Resource not found") => new("not_found", message);

    /// <summary>
    /// Predefined error for unauthorized access.
    /// </summary>
    public static Error Unauthorized(string message = "Access denied") => new("unauthorized", message);

    /// <summary>
    /// Predefined error for forbidden operations.
    /// </summary>
    public static Error Forbidden(string message = "Operation forbidden") => new("forbidden", message);

    /// <summary>
    /// Predefined error for conflict scenarios (e.g., duplicate resource).
    /// </summary>
    public static Error Conflict(string message = "Resource conflict") => new("conflict", message);

    /// <summary>
    /// Predefined error for internal server errors.
    /// </summary>
    public static Error Internal(string message = "An unexpected error occurred") => new("internal_error", message);
}
