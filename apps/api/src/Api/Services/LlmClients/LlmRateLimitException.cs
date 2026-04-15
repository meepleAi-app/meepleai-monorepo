using System.Net;

namespace Api.Services.LlmClients;

/// <summary>
/// Thrown when an LLM provider returns a rate-limit (429) response.
/// Enables callers to distinguish transient rate limits from permanent errors.
/// </summary>
public sealed class LlmRateLimitException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public LlmRateLimitException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public LlmRateLimitException(string message, HttpStatusCode statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
