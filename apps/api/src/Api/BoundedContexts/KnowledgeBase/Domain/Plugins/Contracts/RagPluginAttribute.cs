// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;

/// <summary>
/// Attribute for marking classes as RAG plugins for automatic discovery.
/// </summary>
/// <remarks>
/// Apply this attribute to plugin classes to enable automatic registration
/// by the plugin registry during assembly scanning.
/// </remarks>
/// <example>
/// <code>
/// [RagPlugin("routing-intent-v1", Category = PluginCategory.Routing)]
/// public class IntentRoutingPlugin : RagPluginBase
/// {
///     // Plugin implementation
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RagPluginAttribute : Attribute
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Functional category of the plugin.
    /// </summary>
    public PluginCategory Category { get; set; } = PluginCategory.Transform;

    /// <summary>
    /// Human-readable name for the plugin.
    /// If not specified, derived from the class name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Plugin version. Defaults to "1.0.0".
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Author or team responsible for the plugin.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Whether this is a built-in plugin.
    /// </summary>
    public bool IsBuiltIn { get; set; } = true;

    /// <summary>
    /// Priority for execution ordering (lower = higher priority).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Creates a new RagPluginAttribute with the specified ID.
    /// </summary>
    /// <param name="id">Unique identifier for the plugin.</param>
    public RagPluginAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }
}
