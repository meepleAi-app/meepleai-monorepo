#pragma warning disable MA0048 // File name must match type name - Contains related types
namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Identifies the type of OpenRouter rate limit error received in a 429 or 402 response.
/// Issue #5087: Free model quota tracking — critical for RPM vs RPD distinction.
/// </summary>
public enum RateLimitErrorType
{
    /// <summary>
    /// RPM limit hit — retry after ~60 seconds (X-RateLimit-Reset ≤ 60s from now).
    /// Message contains "limit_rpm".
    /// </summary>
    Rpm,

    /// <summary>
    /// RPD (daily) limit hit — retry after midnight UTC (X-RateLimit-Reset = next midnight).
    /// Message contains "limit_rpd".
    /// </summary>
    Rpd,

    /// <summary>
    /// HTTP 402 Payment Required — balance is negative, block all OpenRouter requests.
    /// </summary>
    PaymentRequired,

    /// <summary>
    /// 429 received but message does not match "limit_rpm" or "limit_rpd".
    /// </summary>
    Unknown
}

/// <summary>
/// Parsed rate limit error from an OpenRouter 429 or 402 response.
/// Issue #5087: Free model quota tracking — parsed from error body and response headers.
/// </summary>
/// <param name="ErrorType">Classified rate limit type.</param>
/// <param name="ResetTimestampMs">Unix millisecond timestamp from X-RateLimit-Reset header (when quota resets).</param>
/// <param name="ModelId">Model ID extracted from the error message (e.g. "meta-llama/llama-3.3-70b-instruct:free").</param>
/// <param name="Limit">Rate limit ceiling from X-RateLimit-Limit header.</param>
/// <param name="Remaining">Remaining quota from X-RateLimit-Remaining header.</param>
internal sealed record OpenRouterRateLimitError(
    RateLimitErrorType ErrorType,
    long? ResetTimestampMs,
    string? ModelId,
    int? Limit,
    int? Remaining);
