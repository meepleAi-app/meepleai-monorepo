namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// File-based logger for OpenRouter requests, rate limit hits, and circuit breaker events.
/// Issue #5073: Rotating JSONL log with 30-day retention for debugging and audit.
/// </summary>
public interface IOpenRouterFileLogger
{
    /// <summary>
    /// Logs a completed LLM request (success or failure) to the rotating file.
    /// </summary>
    void LogRequest(
        string requestId,
        string model,
        string provider,
        string source,
        Guid? userId,
        int promptTokens,
        int completionTokens,
        decimal costUsd,
        long latencyMs,
        bool success,
        bool isFreeModel,
        string? sessionId,
        string? errorMessage = null);

    /// <summary>
    /// Logs a rate limit hit event (HTTP 429) to the rotating file.
    /// </summary>
    void LogRateLimitHit(
        string model,
        int httpStatus,
        int retryAfterMs,
        int currentRpm,
        int limitRpm);

    /// <summary>
    /// Logs a circuit breaker state change (open/close) to the rotating file.
    /// </summary>
    void LogCircuitBreakerEvent(
        string provider,
        string newState,
        int consecutiveFailures);
}
