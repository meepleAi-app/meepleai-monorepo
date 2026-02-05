// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3417 - Plugin Registry Service
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Registry;

/// <summary>
/// Aggregated health report for all registered plugins.
/// </summary>
public sealed record PluginHealthReport
{
    /// <summary>
    /// Overall health status across all plugins.
    /// </summary>
    public required HealthStatus OverallStatus { get; init; }

    /// <summary>
    /// Total number of registered plugins.
    /// </summary>
    public required int TotalPlugins { get; init; }

    /// <summary>
    /// Number of healthy plugins.
    /// </summary>
    public required int HealthyCount { get; init; }

    /// <summary>
    /// Number of degraded plugins.
    /// </summary>
    public required int DegradedCount { get; init; }

    /// <summary>
    /// Number of unhealthy plugins.
    /// </summary>
    public required int UnhealthyCount { get; init; }

    /// <summary>
    /// Number of disabled plugins.
    /// </summary>
    public required int DisabledCount { get; init; }

    /// <summary>
    /// Individual plugin health results.
    /// </summary>
    public required IReadOnlyDictionary<string, PluginHealthEntry> Plugins { get; init; }

    /// <summary>
    /// When the health report was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Time taken to generate the report in milliseconds.
    /// </summary>
    public double ReportDurationMs { get; init; }
}

/// <summary>
/// Health entry for an individual plugin in the report.
/// </summary>
public sealed record PluginHealthEntry
{
    /// <summary>
    /// Plugin metadata.
    /// </summary>
    public required PluginMetadata Metadata { get; init; }

    /// <summary>
    /// Health check result for the plugin.
    /// </summary>
    public required HealthCheckResult HealthResult { get; init; }

    /// <summary>
    /// Whether the plugin is currently enabled.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Last successful execution time, if tracked.
    /// </summary>
    public DateTimeOffset? LastSuccessfulExecution { get; init; }

    /// <summary>
    /// Recent error count, if tracked.
    /// </summary>
    public int? RecentErrorCount { get; init; }
}
