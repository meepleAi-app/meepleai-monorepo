namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when authentication is required but not provided or invalid.
/// Maps to HTTP 401 Unauthorized.
/// Note: Named UnauthorizedHttpException to avoid conflict with System.UnauthorizedAccessException.
/// </summary>
public class UnauthorizedHttpException : HttpException
{
    [SetsRequiredMembers]
    public UnauthorizedHttpException(string message)
        : base(StatusCodes.Status401Unauthorized, "unauthorized", message)
    {
    }

    [SetsRequiredMembers]
    public UnauthorizedHttpException(string message, Exception innerException)
        : base(StatusCodes.Status401Unauthorized, "unauthorized", message, innerException)
    {
    }

    [SetsRequiredMembers]
    public UnauthorizedHttpException()
        : base(StatusCodes.Status401Unauthorized, "unauthorized", "Authentication required")
    {
    }
}

