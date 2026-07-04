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

    /// <summary>
    /// Creates a 409 with an explicit machine-readable <paramref name="errorCode"/>
    /// so subclasses (e.g. <see cref="Api.BoundedContexts.GameManagement.Domain.Exceptions.MaxLiveSessionsExceededException"/>)
    /// can carry a discriminable code into the HTTP body (WS1 DEC-7).
    /// </summary>
    [SetsRequiredMembers]
    public ConflictException(string errorCode, string message)
        : base(StatusCodes.Status409Conflict, errorCode, message)
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
