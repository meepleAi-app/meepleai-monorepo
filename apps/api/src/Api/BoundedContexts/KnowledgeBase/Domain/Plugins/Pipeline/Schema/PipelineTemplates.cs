// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3416 - Pipeline Definition Schema
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Schema;

/// <summary>
/// Provides pre-defined pipeline templates for common RAG strategies.
/// </summary>
public static class PipelineTemplates
{
    /// <summary>
    /// FAST pipeline - prioritizes speed over accuracy.
    /// Single retrieval, no reranking, fast generation.
    /// </summary>
    public static PipelineDefinition Fast => new()
    {
        SchemaVersion = "1.0",
        Id = "rag-fast-v1",
        Name = "FAST RAG Pipeline",
        Version = "1.0.0",
        Description = "Optimized for speed. Single retrieval pass with basic filtering.",
        Nodes =
        [
            new PipelineNode
            {
                Id = "router",
                PluginId = "routing-intent-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "retrieval",
                PluginId = "retrieval-vector-v1",
                TimeoutMs = 10000
            },
            new PipelineNode
            {
                Id = "generation",
                PluginId = "generation-fast-v1",
                TimeoutMs = 30000
            }
        ],
        Edges =
        [
            new PipelineEdge { From = "router", To = "retrieval", Condition = "always" },
            new PipelineEdge { From = "retrieval", To = "generation", Condition = "always" }
        ],
        EntryPoint = "router",
        ExitPoints = ["generation"],
        GlobalTimeoutMs = 60000,
        MaxParallelism = 3,
        Tags = ["fast", "basic", "low-latency"]
    };

    /// <summary>
    /// BALANCED pipeline - balances speed and accuracy.
    /// Hybrid retrieval with basic reranking.
    /// </summary>
    public static PipelineDefinition Balanced => new()
    {
        SchemaVersion = "1.0",
        Id = "rag-balanced-v1",
        Name = "BALANCED RAG Pipeline",
        Version = "1.0.0",
        Description = "Balanced approach with hybrid retrieval and reranking.",
        Nodes =
        [
            new PipelineNode
            {
                Id = "router",
                PluginId = "routing-intent-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "vector-retrieval",
                PluginId = "retrieval-vector-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "keyword-retrieval",
                PluginId = "retrieval-keyword-v1",
                TimeoutMs = 10000
            },
            new PipelineNode
            {
                Id = "merge",
                PluginId = "transform-merge-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "rerank",
                PluginId = "evaluation-rerank-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "generation",
                PluginId = "generation-balanced-v1",
                TimeoutMs = 45000
            }
        ],
        Edges =
        [
            new PipelineEdge { From = "router", To = "vector-retrieval", Condition = "always" },
            new PipelineEdge { From = "router", To = "keyword-retrieval", Condition = "always" },
            new PipelineEdge { From = "vector-retrieval", To = "merge", Condition = "always" },
            new PipelineEdge { From = "keyword-retrieval", To = "merge", Condition = "always" },
            new PipelineEdge { From = "merge", To = "rerank", Condition = "always" },
            new PipelineEdge { From = "rerank", To = "generation", Condition = "always" }
        ],
        EntryPoint = "router",
        ExitPoints = ["generation"],
        GlobalTimeoutMs = 90000,
        MaxParallelism = 5,
        Tags = ["balanced", "hybrid", "reranking"]
    };

    /// <summary>
    /// PRECISE pipeline - prioritizes accuracy over speed.
    /// Multi-stage retrieval with cross-encoder reranking and validation.
    /// </summary>
    public static PipelineDefinition Precise => new()
    {
        SchemaVersion = "1.0",
        Id = "rag-precise-v1",
        Name = "PRECISE RAG Pipeline",
        Version = "1.0.0",
        Description = "Maximum accuracy with multi-stage retrieval and validation.",
        Nodes =
        [
            new PipelineNode
            {
                Id = "router",
                PluginId = "routing-intent-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "query-expand",
                PluginId = "transform-query-expand-v1",
                TimeoutMs = 10000
            },
            new PipelineNode
            {
                Id = "vector-retrieval",
                PluginId = "retrieval-vector-v1",
                TimeoutMs = 20000
            },
            new PipelineNode
            {
                Id = "keyword-retrieval",
                PluginId = "retrieval-keyword-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "semantic-retrieval",
                PluginId = "retrieval-semantic-v1",
                TimeoutMs = 20000
            },
            new PipelineNode
            {
                Id = "merge",
                PluginId = "transform-merge-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "rerank",
                PluginId = "evaluation-cross-encoder-v1",
                TimeoutMs = 20000
            },
            new PipelineNode
            {
                Id = "validate",
                PluginId = "validation-relevance-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "generation",
                PluginId = "generation-precise-v1",
                TimeoutMs = 60000
            },
            new PipelineNode
            {
                Id = "verify",
                PluginId = "validation-hallucination-v1",
                TimeoutMs = 20000
            }
        ],
        Edges =
        [
            new PipelineEdge { From = "router", To = "query-expand", Condition = "always" },
            new PipelineEdge { From = "query-expand", To = "vector-retrieval", Condition = "always" },
            new PipelineEdge { From = "query-expand", To = "keyword-retrieval", Condition = "always" },
            new PipelineEdge { From = "query-expand", To = "semantic-retrieval", Condition = "always" },
            new PipelineEdge { From = "vector-retrieval", To = "merge", Condition = "always" },
            new PipelineEdge { From = "keyword-retrieval", To = "merge", Condition = "always" },
            new PipelineEdge { From = "semantic-retrieval", To = "merge", Condition = "always" },
            new PipelineEdge { From = "merge", To = "rerank", Condition = "always" },
            new PipelineEdge { From = "rerank", To = "validate", Condition = "always" },
            new PipelineEdge { From = "validate", To = "generation", Condition = "confidence >= 0.5" },
            new PipelineEdge { From = "generation", To = "verify", Condition = "always" }
        ],
        EntryPoint = "router",
        ExitPoints = ["verify"],
        GlobalTimeoutMs = 180000,
        MaxParallelism = 7,
        Tags = ["precise", "multi-stage", "validated"]
    };

    /// <summary>
    /// GAME_RULES pipeline - specialized for board game rule queries.
    /// Routes between rules, FAQ, and setup content.
    /// </summary>
    public static PipelineDefinition GameRules => new()
    {
        SchemaVersion = "1.0",
        Id = "rag-game-rules-v1",
        Name = "Game Rules RAG Pipeline",
        Version = "1.0.0",
        Description = "Specialized for board game rule queries with content-type routing.",
        Nodes =
        [
            new PipelineNode
            {
                Id = "router",
                PluginId = "routing-game-content-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "rules-retrieval",
                PluginId = "retrieval-rules-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "faq-retrieval",
                PluginId = "retrieval-faq-v1",
                TimeoutMs = 10000
            },
            new PipelineNode
            {
                Id = "setup-retrieval",
                PluginId = "retrieval-setup-v1",
                TimeoutMs = 10000
            },
            new PipelineNode
            {
                Id = "rerank",
                PluginId = "evaluation-game-relevance-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "generation",
                PluginId = "generation-game-expert-v1",
                TimeoutMs = 45000
            }
        ],
        Edges =
        [
            new PipelineEdge { From = "router", To = "rules-retrieval", Condition = "output.type == 'rules'", Priority = 1 },
            new PipelineEdge { From = "router", To = "faq-retrieval", Condition = "output.type == 'faq'", Priority = 2 },
            new PipelineEdge { From = "router", To = "setup-retrieval", Condition = "output.type == 'setup'", Priority = 3 },
            new PipelineEdge { From = "rules-retrieval", To = "rerank", Condition = "always" },
            new PipelineEdge { From = "faq-retrieval", To = "rerank", Condition = "always" },
            new PipelineEdge { From = "setup-retrieval", To = "rerank", Condition = "always" },
            new PipelineEdge { From = "rerank", To = "generation", Condition = "always" }
        ],
        EntryPoint = "router",
        ExitPoints = ["generation"],
        GlobalTimeoutMs = 90000,
        MaxParallelism = 4,
        Tags = ["game-rules", "domain-specific", "routed"]
    };

    /// <summary>
    /// CACHED pipeline - optimized for repeated queries.
    /// Checks cache before retrieval.
    /// </summary>
    public static PipelineDefinition Cached => new()
    {
        SchemaVersion = "1.0",
        Id = "rag-cached-v1",
        Name = "CACHED RAG Pipeline",
        Version = "1.0.0",
        Description = "Cache-first pipeline for high-traffic queries.",
        Nodes =
        [
            new PipelineNode
            {
                Id = "cache-check",
                PluginId = "cache-lookup-v1",
                TimeoutMs = 2000
            },
            new PipelineNode
            {
                Id = "router",
                PluginId = "routing-intent-v1",
                TimeoutMs = 5000
            },
            new PipelineNode
            {
                Id = "retrieval",
                PluginId = "retrieval-hybrid-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "rerank",
                PluginId = "evaluation-rerank-v1",
                TimeoutMs = 15000
            },
            new PipelineNode
            {
                Id = "generation",
                PluginId = "generation-balanced-v1",
                TimeoutMs = 45000
            },
            new PipelineNode
            {
                Id = "cache-store",
                PluginId = "cache-store-v1",
                TimeoutMs = 2000
            }
        ],
        Edges =
        [
            new PipelineEdge { From = "cache-check", To = "router", Condition = "success == false", Priority = 1 },
            new PipelineEdge { From = "cache-check", To = "generation", Condition = "success == true", Priority = 0 },
            new PipelineEdge { From = "router", To = "retrieval", Condition = "always" },
            new PipelineEdge { From = "retrieval", To = "rerank", Condition = "always" },
            new PipelineEdge { From = "rerank", To = "generation", Condition = "always" },
            new PipelineEdge { From = "generation", To = "cache-store", Condition = "success == true" }
        ],
        EntryPoint = "cache-check",
        ExitPoints = ["generation", "cache-store"],
        GlobalTimeoutMs = 90000,
        MaxParallelism = 4,
        Tags = ["cached", "high-traffic", "optimized"]
    };

    /// <summary>
    /// Gets all available pipeline templates.
    /// </summary>
    public static IReadOnlyList<PipelineDefinition> All =>
    [
        Fast,
        Balanced,
        Precise,
        GameRules,
        Cached
    ];

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <returns>The template, or null if not found.</returns>
    public static PipelineDefinition? GetById(string id)
    {
        return All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
