// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

/// <summary>
/// Represents a node in the pipeline DAG.
/// Each node references a plugin to execute with its configuration.
/// </summary>
public sealed record PipelineNode
{
    /// <summary>
    /// Unique identifier for this node within the pipeline.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The plugin ID to execute at this node.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Plugin-specific configuration for this node.
    /// </summary>
    public PluginConfig? Config { get; init; }

    /// <summary>
    /// Timeout in milliseconds for this node's execution.
    /// Defaults to 30000 (30 seconds).
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Retry configuration for this node.
    /// </summary>
    public RetryConfig? Retry { get; init; }

    /// <summary>
    /// Human-readable name for this node.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Description of what this node does.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Retry configuration for a pipeline node.
/// </summary>
public sealed record RetryConfig
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Base backoff delay in milliseconds.
    /// </summary>
    public int BackoffMs { get; init; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff.
    /// </summary>
    public bool ExponentialBackoff { get; init; } = true;

    /// <summary>
    /// Maximum backoff delay in milliseconds.
    /// </summary>
    public int MaxBackoffMs { get; init; } = 30000;
}
