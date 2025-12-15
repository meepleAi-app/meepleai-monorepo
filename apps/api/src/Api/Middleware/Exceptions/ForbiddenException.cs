namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a user is authenticated but lacks permission for the requested operation.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : HttpException
{
    [SetsRequiredMembers]
    public ForbiddenException(string message = "Access denied")
        : base(StatusCodes.Status403Forbidden, "forbidden", message)
    {
    }

    [SetsRequiredMembers]
    public ForbiddenException(string message, Exception innerException)
        : base(StatusCodes.Status403Forbidden, "forbidden", message, innerException)
    {
    }
    public ForbiddenException()
    {
    }
}
