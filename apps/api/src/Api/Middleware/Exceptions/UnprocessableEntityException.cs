namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a request is well-formed but semantically invalid
/// (e.g. pre-conditions not met). Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public class UnprocessableEntityException : HttpException
{
    [SetsRequiredMembers]
    public UnprocessableEntityException(string message)
        : base(StatusCodes.Status422UnprocessableEntity, "unprocessable_entity", message)
    {
    }

    [SetsRequiredMembers]
    public UnprocessableEntityException(string errorCode, string message)
        : base(StatusCodes.Status422UnprocessableEntity, errorCode, message)
    {
    }

    [SetsRequiredMembers]
    public UnprocessableEntityException(string message, Exception innerException)
        : base(StatusCodes.Status422UnprocessableEntity, "unprocessable_entity", message, innerException)
    {
    }

    public UnprocessableEntityException()
    {
    }
}
