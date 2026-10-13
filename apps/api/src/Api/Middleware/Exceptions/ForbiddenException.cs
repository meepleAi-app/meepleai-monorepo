namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a user is authenticated but lacks permission for the requested operation.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : HttpException
{
    [SetsRequiredMembers]
    public ForbiddenException(string message)
        : base(StatusCodes.Status403Forbidden, "forbidden", message)
    {
    }

    [SetsRequiredMembers]
    public ForbiddenException(string message, Exception innerException)
        : base(StatusCodes.Status403Forbidden, "forbidden", message, innerException)
    {
    }
    [SetsRequiredMembers]
    public ForbiddenException()
        : base(StatusCodes.Status403Forbidden, "forbidden", "Access denied")
    {
    }

    /// <summary>
    /// Per i dinieghi che il client deve distinguere dal 403 generico: resta un 403 per chi
    /// intercetta <see cref="ForbiddenException"/>, ma con un codice proprio.
    /// </summary>
    [SetsRequiredMembers]
    protected ForbiddenException(string errorCode, string message)
        : base(StatusCodes.Status403Forbidden, errorCode, message)
    {
    }
}
