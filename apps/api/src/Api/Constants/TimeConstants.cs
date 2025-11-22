namespace Api.Constants;

/// <summary>
/// Centralized time-related constants to eliminate magic numbers.
/// All values are in seconds unless otherwise noted.
/// </summary>
public static class TimeConstants
{
    /// <summary>
    /// One day in seconds (86400).
    /// Used for cache TTL, session expiry, and other 24-hour intervals.
    /// </summary>
    public const int OneDayInSeconds = 86400;

    /// <summary>
    /// One hour in seconds (3600).
    /// Used for cache TTL and rate limiting windows.
    /// </summary>
    public const int OneHourInSeconds = 3600;

    /// <summary>
    /// Default cache TTL for AI responses (24 hours).
    /// </summary>
    public const int DefaultAiResponseCacheTtlSeconds = OneDayInSeconds;

    /// <summary>
    /// Default cache TTL for prompt templates (1 hour).
    /// </summary>
    public const int PromptTemplateCacheTtlSeconds = OneHourInSeconds;

    /// <summary>
    /// Rate limit refill window (1 hour).
    /// </summary>
    public const int RateLimitRefillWindowSeconds = OneHourInSeconds;
}
