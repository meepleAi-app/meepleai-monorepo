// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

/// <summary>
/// Represents the input data for a plugin execution.
/// Contains the payload, context from previous pipeline stages, and execution metadata.
/// </summary>
public sealed record PluginInput
{
    /// <summary>
    /// Unique identifier for this execution request.
    /// </summary>
    public required Guid ExecutionId { get; init; }

    /// <summary>
    /// The primary data payload for the plugin to process.
    /// Structure depends on the plugin's InputSchema.
    /// </summary>
    public required JsonDocument Payload { get; init; }

    /// <summary>
    /// Context data from previous pipeline stages.
    /// Key is the source node ID, value is the output from that node.
    /// </summary>
    public IReadOnlyDictionary<string, JsonDocument> PipelineContext { get; init; } = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);

    /// <summary>
    /// Additional metadata for the execution.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// The user ID associated with this request, if available.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The game ID associated with this request, if available.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// The timestamp when this input was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a simple PluginInput with just a payload.
    /// </summary>
    public static PluginInput Create(JsonDocument payload)
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
    }

    /// <summary>
    /// Creates a PluginInput with payload and context.
    /// </summary>
    public static PluginInput Create(
        JsonDocument payload,
        IReadOnlyDictionary<string, JsonDocument> context,
        Guid? userId = null,
        Guid? gameId = null)
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload,
            PipelineContext = context,
            UserId = userId,
            GameId = gameId
        };
    }
}
