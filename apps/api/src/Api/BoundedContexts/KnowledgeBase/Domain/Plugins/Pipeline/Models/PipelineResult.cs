// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

/// <summary>
/// Result of a pipeline execution.
/// </summary>
public sealed record PipelineResult
{
    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    public required Guid ExecutionId { get; init; }

    /// <summary>
    /// The pipeline ID that was executed.
    /// </summary>
    public required string PipelineId { get; init; }

    /// <summary>
    /// Whether the pipeline executed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Outputs from exit point nodes.
    /// </summary>
    public required IReadOnlyDictionary<string, PluginOutput> FinalOutputs { get; init; }

    /// <summary>
    /// Execution metrics for each node.
    /// </summary>
    public required IReadOnlyDictionary<string, PluginExecutionMetrics> NodeMetrics { get; init; }

    /// <summary>
    /// Total pipeline execution duration in milliseconds.
    /// </summary>
    public required double TotalDurationMs { get; init; }

    /// <summary>
    /// Number of nodes that executed successfully.
    /// </summary>
    public int NodesExecuted { get; init; }

    /// <summary>
    /// Number of nodes that failed.
    /// </summary>
    public int NodesFailed { get; init; }

    /// <summary>
    /// Number of nodes that were skipped.
    /// </summary>
    public int NodesSkipped { get; init; }

    /// <summary>
    /// Error message if the pipeline failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The node that caused the failure, if any.
    /// </summary>
    public string? FailedNodeId { get; init; }

    /// <summary>
    /// Total input tokens used across all nodes.
    /// </summary>
    public int TotalInputTokens => NodeMetrics.Values.Sum(m => m.InputTokens ?? 0);

    /// <summary>
    /// Total output tokens used across all nodes.
    /// </summary>
    public int TotalOutputTokens => NodeMetrics.Values.Sum(m => m.OutputTokens ?? 0);

    /// <summary>
    /// When the execution completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
}
