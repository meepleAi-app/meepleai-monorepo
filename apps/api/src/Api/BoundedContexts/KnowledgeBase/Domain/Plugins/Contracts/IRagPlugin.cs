// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;

/// <summary>
/// Core contract for all RAG pipeline plugins.
/// Plugins implement this interface to participate in the RAG workflow.
/// </summary>
/// <remarks>
/// <para>
/// Each plugin must:
/// - Have a unique ID within its category
/// - Define input/output/config JSON schemas for validation
/// - Implement execution logic
/// - Support health checks
/// - Validate configuration
/// </para>
/// <para>
/// Plugins are discovered, registered, and orchestrated by the plugin registry
/// and DAG orchestrator respectively.
/// </para>
/// </remarks>
public interface IRagPlugin
{
    /// <summary>
    /// Unique identifier for this plugin.
    /// Format: {category}-{name}-v{version} (e.g., "routing-intent-v1")
    /// Must be unique within the registry.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable display name for the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Semantic version of the plugin (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Functional category of the plugin.
    /// Determines where in the pipeline this plugin can be used.
    /// </summary>
    PluginCategory Category { get; }

    /// <summary>
    /// JSON Schema defining the expected input structure.
    /// Used for runtime validation of plugin inputs.
    /// </summary>
    JsonDocument InputSchema { get; }

    /// <summary>
    /// JSON Schema defining the output structure.
    /// Used for documentation and downstream validation.
    /// </summary>
    JsonDocument OutputSchema { get; }

    /// <summary>
    /// JSON Schema defining the configuration structure.
    /// Used to validate plugin configuration.
    /// </summary>
    JsonDocument ConfigSchema { get; }

    /// <summary>
    /// Plugin metadata for discovery and documentation.
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// Executes the plugin logic with the given input.
    /// </summary>
    /// <param name="input">The input data to process.</param>
    /// <param name="config">Plugin configuration for this execution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The plugin output containing results or error information.</returns>
    Task<PluginOutput> ExecuteAsync(
        PluginInput input,
        PluginConfig? config = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on the plugin.
    /// Should verify all dependencies and resources are available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Health check result indicating the plugin's operational status.</returns>
    Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the given configuration for this plugin.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValidationResult ValidateConfig(PluginConfig config);

    /// <summary>
    /// Validates the input data before execution.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <returns>Validation result with any errors or warnings.</returns>
    ValidationResult ValidateInput(PluginInput input);
}
