namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Base exception for HTTP-specific errors with status codes.
/// Provides a consistent way to throw exceptions that map to specific HTTP responses.
/// </summary>
public class HttpException : Exception
{
    /// <summary>
    /// Gets the HTTP status code for this exception.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets a machine-readable error code (e.g., "validation_error", "not_found").
    /// </summary>
    public required string ErrorCode { get; init; }

    [SetsRequiredMembers]
    public HttpException(
        int statusCode,
        string errorCode,
        string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    [SetsRequiredMembers]
    public HttpException(
        int statusCode,
        string errorCode,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
    public HttpException()
    {
    }
}
