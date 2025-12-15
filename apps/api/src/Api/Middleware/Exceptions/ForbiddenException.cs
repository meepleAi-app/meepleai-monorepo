namespace Api.Middleware.Exceptions;

/// <summary>
/// Exception thrown when a user is authenticated but lacks permission for the requested operation.
/// Maps to HTTP 403 Forbidden.
/// </summary>
internal class ForbiddenException : HttpException
{
    public ForbiddenException(string message = "Access denied")
        : base(StatusCodes.Status403Forbidden, "forbidden", message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(StatusCodes.Status403Forbidden, "forbidden", message, innerException)
    {
    }
    public ForbiddenException()
    {
    }
}
