// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

/// <summary>
/// Defines a complete pipeline as a DAG of nodes and edges.
/// </summary>
public sealed record PipelineDefinition
{
    /// <summary>
    /// Schema version for this pipeline definition format.
    /// Used for forward compatibility with schema changes.
    /// </summary>
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// Unique identifier for this pipeline.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name for the pipeline.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Semantic version of the pipeline definition.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Description of the pipeline's purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The nodes in this pipeline.
    /// </summary>
    public required IReadOnlyList<PipelineNode> Nodes { get; init; }

    /// <summary>
    /// The edges connecting nodes in this pipeline.
    /// </summary>
    public required IReadOnlyList<PipelineEdge> Edges { get; init; }

    /// <summary>
    /// The entry point node ID (starting node).
    /// </summary>
    public required string EntryPoint { get; init; }

    /// <summary>
    /// The exit point node IDs (terminal nodes).
    /// </summary>
    public required IReadOnlyList<string> ExitPoints { get; init; }

    /// <summary>
    /// Global timeout for the entire pipeline in milliseconds.
    /// </summary>
    public int GlobalTimeoutMs { get; init; } = 120000;

    /// <summary>
    /// Maximum number of parallel node executions.
    /// </summary>
    public int MaxParallelism { get; init; } = 5;

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// When the pipeline was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
