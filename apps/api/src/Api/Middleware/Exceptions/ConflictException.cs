namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a request conflicts with the current state of the resource.
/// Typically used for duplicate resources or constraint violations.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class ConflictException : HttpException
{
    [SetsRequiredMembers]
    public ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "conflict", message)
    {
    }

    [SetsRequiredMembers]
    public ConflictException(string message, Exception innerException)
        : base(StatusCodes.Status409Conflict, "conflict", message, innerException)
    {
    }
    public ConflictException()
    {
    }
}
