namespace Api.Middleware.Exceptions;

/// <summary>
/// Exception thrown when a client request is malformed or invalid.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class BadRequestException : HttpException
{
    public BadRequestException(string message)
        : base(StatusCodes.Status400BadRequest, "bad_request", message)
    {
    }

    public BadRequestException(string message, Exception innerException)
        : base(StatusCodes.Status400BadRequest, "bad_request", message, innerException)
    {
    }
}
