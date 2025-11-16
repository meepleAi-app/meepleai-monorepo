namespace Api.Middleware.Exceptions;

/// <summary>
/// Exception thrown when authentication is required but not provided or invalid.
/// Maps to HTTP 401 Unauthorized.
/// Note: Named UnauthorizedHttpException to avoid conflict with System.UnauthorizedAccessException.
/// </summary>
public class UnauthorizedHttpException : HttpException
{
    public UnauthorizedHttpException(string message = "Authentication required")
        : base(StatusCodes.Status401Unauthorized, "unauthorized", message)
    {
    }

    public UnauthorizedHttpException(string message, Exception innerException)
        : base(StatusCodes.Status401Unauthorized, "unauthorized", message, innerException)
    {
    }
}
