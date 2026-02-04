// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

/// <summary>
/// Represents the output from a plugin execution.
/// Contains the result, metrics, and any routing decisions.
/// </summary>
public sealed record PluginOutput
{
    /// <summary>
    /// The execution ID this output corresponds to.
    /// </summary>
    public required Guid ExecutionId { get; init; }

    /// <summary>
    /// Indicates whether the plugin execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The result data from the plugin execution.
    /// Structure depends on the plugin's OutputSchema.
    /// </summary>
    public JsonDocument? Result { get; init; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code for categorizing failures.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Confidence score for the result (0.0 to 1.0).
    /// Used for routing decisions and quality evaluation.
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Execution metrics for monitoring and optimization.
    /// </summary>
    public PluginExecutionMetrics Metrics { get; init; } = new();

    /// <summary>
    /// Additional metadata produced by the plugin.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// The timestamp when this output was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful output with a result.
    /// </summary>
    public static PluginOutput Successful(Guid executionId, JsonDocument result, double? confidence = null)
    {
        return new PluginOutput
        {
            ExecutionId = executionId,
            Success = true,
            Result = result,
            Confidence = confidence
        };
    }

    /// <summary>
    /// Creates a failed output with an error message.
    /// </summary>
    public static PluginOutput Failed(Guid executionId, string errorMessage, string? errorCode = null)
    {
        return new PluginOutput
        {
            ExecutionId = executionId,
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }
}

/// <summary>
/// Execution metrics for a plugin invocation.
/// </summary>
public sealed record PluginExecutionMetrics
{
    /// <summary>
    /// Total execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; init; }

    /// <summary>
    /// Number of input tokens processed (for LLM-based plugins).
    /// </summary>
    public int? InputTokens { get; init; }

    /// <summary>
    /// Number of output tokens generated (for LLM-based plugins).
    /// </summary>
    public int? OutputTokens { get; init; }

    /// <summary>
    /// Number of documents or items processed.
    /// </summary>
    public int? ItemsProcessed { get; init; }

    /// <summary>
    /// Memory usage in bytes, if available.
    /// </summary>
    public long? MemoryUsageBytes { get; init; }

    /// <summary>
    /// Whether the result was served from cache.
    /// </summary>
    public bool CacheHit { get; init; }

    /// <summary>
    /// Number of retry attempts, if any.
    /// </summary>
    public int RetryCount { get; init; }
}
