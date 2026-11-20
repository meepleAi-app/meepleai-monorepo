namespace Api.Middleware.Exceptions;

/// <summary>
/// A permission the code refuses (#5082). The client reads 403 "Access denied" and the message stays in the log, as
/// when these sites threw <see cref="UnauthorizedAccessException"/>: that one now means the file system refused, a
/// server fault, and maps to 500. Not a <see cref="ForbiddenException"/>, which sends its message to the client.
/// </summary>
public sealed class AccessDeniedException : Exception
{
    public AccessDeniedException()
        : base("Access denied")
    {
    }

    public AccessDeniedException(string message)
        : base(message)
    {
    }

    public AccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
