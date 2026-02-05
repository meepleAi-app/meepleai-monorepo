// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3417 - Plugin Registry Service
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Registry;

/// <summary>
/// Registry for discovering, loading, and managing RAG plugins.
/// </summary>
public interface IPluginRegistry
{
    #region Discovery

    /// <summary>
    /// Gets all registered plugins.
    /// </summary>
    IReadOnlyList<PluginMetadata> GetAllPlugins();

    /// <summary>
    /// Gets plugins filtered by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    IReadOnlyList<PluginMetadata> GetPluginsByCategory(PluginCategory category);

    /// <summary>
    /// Gets a specific plugin by ID and optional version.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="version">Optional version. If null, returns the latest version.</param>
    /// <returns>The plugin metadata, or null if not found.</returns>
    PluginMetadata? GetPlugin(string pluginId, string? version = null);

    /// <summary>
    /// Checks if a plugin is registered.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <param name="version">Optional version to check for.</param>
    bool IsRegistered(string pluginId, string? version = null);

    #endregion

    #region Loading

    /// <summary>
    /// Loads and returns a plugin instance.
    /// </summary>
    /// <param name="pluginId">The plugin ID to load.</param>
    /// <param name="version">Optional version. If null, loads the latest version.</param>
    /// <returns>The plugin instance.</returns>
    /// <exception cref="PluginNotFoundException">If the plugin is not found.</exception>
    /// <exception cref="PluginDisabledException">If the plugin is disabled.</exception>
    IRagPlugin LoadPlugin(string pluginId, string? version = null);

    /// <summary>
    /// Tries to load a plugin instance.
    /// </summary>
    /// <param name="pluginId">The plugin ID to load.</param>
    /// <param name="plugin">The loaded plugin if successful.</param>
    /// <param name="version">Optional version. If null, loads the latest version.</param>
    /// <returns>True if the plugin was loaded successfully.</returns>
    bool TryLoadPlugin(string pluginId, out IRagPlugin? plugin, string? version = null);

    #endregion

    #region Health

    /// <summary>
    /// Gets a health report for all registered plugins.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginHealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<HealthCheckResult> CheckPluginHealthAsync(string pluginId, CancellationToken cancellationToken = default);

    #endregion

    #region Management

    /// <summary>
    /// Enables a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to enable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to disable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the plugin registry by rescanning assemblies.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    #endregion
}
