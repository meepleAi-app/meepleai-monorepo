namespace Api.Infrastructure.Entities;

/// <summary>
/// Entity for tracking LLM request costs
/// ISSUE-960: BGAI-018 - Financial cost tracking and attribution
/// </summary>
/// <remarks>
/// Stores financial cost data for all LLM requests (OpenRouter, Ollama).
/// Supports per-user/per-tier cost attribution and cost analytics.
/// </remarks>
public class LlmCostLogEntity
{
    /// <summary>
    /// Unique identifier for this cost log entry
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User who made the request (null for anonymous)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// User role at time of request (for tier-based attribution)
    /// </summary>
    public string UserRole { get; set; } = "Anonymous";

    /// <summary>
    /// Model identifier (e.g., "openai/gpt-4o-mini", "llama3:8b")
    /// </summary>
    public required string ModelId { get; set; }

    /// <summary>
    /// Provider name (OpenRouter, Ollama)
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Number of input/prompt tokens
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Number of output/completion tokens
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens (prompt + completion)
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Cost for input tokens in USD (6 decimal precision)
    /// </summary>
    public decimal InputCost { get; set; }

    /// <summary>
    /// Cost for output tokens in USD (6 decimal precision)
    /// </summary>
    public decimal OutputCost { get; set; }

    /// <summary>
    /// Total cost in USD (input + output)
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Endpoint that made the request (e.g., "chat", "qa", "explain")
    /// </summary>
    public string Endpoint { get; set; } = "unknown";

    /// <summary>
    /// Request success status
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Request latency in milliseconds
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// IP address of requester (for analytics)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent (for analytics)
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp of request (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date for efficient daily aggregation queries
    /// </summary>
    public DateOnly RequestDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Origin of the LLM request (Manual, RagPipeline, EventDriven, AutomatedTest, AgentTask, AdminOperation).
    /// Issue #5071: RequestSource tagging for observability.
    /// </summary>
    public string RequestSource { get; set; } = "Manual";

    // Navigation properties
    public UserEntity? User { get; set; }
}
