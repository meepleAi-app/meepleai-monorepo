namespace Api.Middleware.Exceptions;

/// <summary>
/// Exception thrown when a request conflicts with the current state of the resource.
/// Typically used for duplicate resources or constraint violations.
/// Maps to HTTP 409 Conflict.
/// </summary>
internal class ConflictException : HttpException
{
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "conflict", message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(StatusCodes.Status409Conflict, "conflict", message, innerException)
    {
    }
    public ConflictException()
    {
    }
}
