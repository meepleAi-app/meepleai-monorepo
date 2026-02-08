using System.Diagnostics.CodeAnalysis;

namespace Api.Middleware.Exceptions;

/// <summary>
/// Exception thrown when external service (BGG, etc.) is unavailable.
/// Issue #3120: BGG Integration error handling.
/// Maps to HTTP 503 Service Unavailable.
/// </summary>
public class ExternalServiceException : HttpException
{
    [SetsRequiredMembers]
    public ExternalServiceException(
        string message,
        Exception? innerException = null)
        : base(503, "external_service_unavailable", message, innerException)
    {
    }

    [SetsRequiredMembers]
    public ExternalServiceException(
        string message,
        string errorCode,
        Exception? innerException = null)
        : base(503, errorCode, message, innerException)
    {
    }
}
