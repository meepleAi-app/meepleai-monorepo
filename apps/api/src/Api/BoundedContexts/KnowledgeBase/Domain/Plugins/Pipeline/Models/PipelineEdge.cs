// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3415 - DAG Orchestrator
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

/// <summary>
/// Represents an edge (connection) between two nodes in the pipeline DAG.
/// Edges define the flow of data and can include conditions and transforms.
/// </summary>
public sealed record PipelineEdge
{
    /// <summary>
    /// The source node ID.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// The target node ID.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Condition expression for this edge.
    /// If null or "always", the edge is always followed.
    /// Examples: "confidence >= 0.7", "output.type == 'rules'", "always"
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// Optional transform expression to apply to the output before passing to next node.
    /// </summary>
    public string? Transform { get; init; }

    /// <summary>
    /// Priority for edge evaluation when multiple edges leave a node (lower = higher priority).
    /// </summary>
    public int Priority { get; init; } = 100;
}
