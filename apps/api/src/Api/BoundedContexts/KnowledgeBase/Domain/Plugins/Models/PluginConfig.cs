// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

/// <summary>
/// Base configuration for a plugin instance.
/// Extended by specific plugins for their configuration needs.
/// </summary>
public record PluginConfig
{
    /// <summary>
    /// Whether the plugin is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Timeout for plugin execution in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Maximum number of retry attempts on transient failures.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Backoff delay between retries in milliseconds.
    /// </summary>
    public int RetryBackoffMs { get; init; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff for retries.
    /// </summary>
    public bool ExponentialBackoff { get; init; } = true;

    /// <summary>
    /// Plugin-specific configuration as JSON.
    /// Validated against the plugin's ConfigSchema.
    /// </summary>
    public JsonDocument? CustomConfig { get; init; }

    /// <summary>
    /// Priority for execution ordering (lower = higher priority).
    /// </summary>
    public int Priority { get; init; } = 100;

    /// <summary>
    /// Tags for categorization and filtering.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static PluginConfig Default() => new();

    /// <summary>
    /// Creates a configuration with custom settings.
    /// </summary>
    public static PluginConfig Create(
        bool enabled = true,
        int timeoutMs = 30000,
        int maxRetries = 3,
        JsonDocument? customConfig = null)
    {
        return new PluginConfig
        {
            Enabled = enabled,
            TimeoutMs = timeoutMs,
            MaxRetries = maxRetries,
            CustomConfig = customConfig
        };
    }
}
