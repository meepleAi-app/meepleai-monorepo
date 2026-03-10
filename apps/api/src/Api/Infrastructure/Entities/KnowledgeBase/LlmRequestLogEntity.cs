using Api.Services;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Entity for detailed LLM request logging with 30-day retention.
/// Issue #5072: OpenRouter monitoring — request log persistence.
/// </summary>
/// <remarks>
/// Stores comprehensive request data for analytics dashboards.
/// Records auto-expire after 30 days via LlmRequestLogCleanupJob.
/// </remarks>
public class LlmRequestLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public required string ModelId { get; set; }
    public required string Provider { get; set; }      // "openrouter" | "ollama"
    public string RequestSource { get; set; } = "Manual"; // stored as string from RequestSource enum
    public Guid? UserId { get; set; }
    public string? UserRole { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsStreaming { get; set; }
    public bool IsFreeModel { get; set; }
    public string? SessionId { get; set; }
    public DateTime ExpiresAt { get; set; }            // = RequestedAt + 30 days

    /// <summary>
    /// Issue #5511: GDPR pseudonymization flag.
    /// When true, UserId has been replaced with a salted SHA-256 hash.
    /// </summary>
    public bool IsAnonymized { get; set; }
}
