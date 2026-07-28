namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a caller exceeds a rate limit or quota.
/// Maps to HTTP 429 Too Many Requests via the generic <see cref="HttpException"/> arm
/// of the exception-handling middleware.
/// </summary>
public class TooManyRequestsException : HttpException
{
    [SetsRequiredMembers]
    public TooManyRequestsException(string message)
        : base(StatusCodes.Status429TooManyRequests, "too_many_requests", message)
    {
    }

    /// <summary>
    /// Creates a 429 with an explicit machine-readable <paramref name="errorCode"/>.
    /// </summary>
    [SetsRequiredMembers]
    public TooManyRequestsException(string errorCode, string message)
        : base(StatusCodes.Status429TooManyRequests, errorCode, message)
    {
    }

    [SetsRequiredMembers]
    public TooManyRequestsException(string message, Exception innerException)
        : base(StatusCodes.Status429TooManyRequests, "too_many_requests", message, innerException)
    {
    }

    public TooManyRequestsException()
    {
    }
}
