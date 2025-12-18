namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a client request is malformed or invalid.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class BadRequestException : HttpException
{
    [SetsRequiredMembers]
    public BadRequestException(string message)
        : base(StatusCodes.Status400BadRequest, "bad_request", message)
    {
    }

    [SetsRequiredMembers]
    public BadRequestException(string message, Exception innerException)
        : base(StatusCodes.Status400BadRequest, "bad_request", message, innerException)
    {
    }
    public BadRequestException()
    {
    }
}
