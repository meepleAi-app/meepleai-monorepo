namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a resource has reached a terminal lifecycle state and
/// is no longer addressable (e.g. an expired or cancelled invitation token).
/// Maps to HTTP 410 Gone.
/// </summary>
/// <remarks>
/// Issue #607 (Wave A.5a): introduced for token-based invitation lifecycle —
/// pending-past-expiry, cancelled, and expired states surface as 410 Gone so
/// the frontend can render a terminal banner instead of retrying.
/// </remarks>
public class GoneException : HttpException
{
    [SetsRequiredMembers]
    public GoneException(string message)
        : base(StatusCodes.Status410Gone, "gone", message)
    {
    }

    [SetsRequiredMembers]
    public GoneException(string message, Exception innerException)
        : base(StatusCodes.Status410Gone, "gone", message, innerException)
    {
    }

    public GoneException()
    {
    }
}
