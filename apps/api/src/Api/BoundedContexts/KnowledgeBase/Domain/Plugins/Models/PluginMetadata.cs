// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

/// <summary>
/// Metadata describing a plugin for discovery and documentation purposes.
/// </summary>
public sealed record PluginMetadata
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// Format: {category}-{name}-v{version} (e.g., "routing-intent-v1")
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name for the plugin.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Semantic version of the plugin (e.g., "1.0.0").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Functional category of the plugin.
    /// </summary>
    public required PluginCategory Category { get; init; }

    /// <summary>
    /// Detailed description of the plugin's functionality.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Author or team responsible for the plugin.
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Whether the plugin is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Whether this is a built-in plugin or third-party.
    /// </summary>
    public bool IsBuiltIn { get; init; } = true;

    /// <summary>
    /// Tags for categorization and search.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Plugin capabilities (e.g., "streaming", "batch", "async").
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// Required dependencies (other plugin IDs).
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>
    /// When the plugin was registered.
    /// </summary>
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last time the plugin was updated.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// URL to documentation or source code.
    /// </summary>
    public string? DocumentationUrl { get; init; }

    /// <summary>
    /// Creates minimal metadata for a plugin.
    /// </summary>
    public static PluginMetadata Create(
        string id,
        string name,
        string version,
        PluginCategory category,
        string? description = null)
    {
        return new PluginMetadata
        {
            Id = id,
            Name = name,
            Version = version,
            Category = category,
            Description = description ?? string.Empty
        };
    }
}
