// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

/// <summary>
/// Result of a plugin health check.
/// </summary>
public sealed record HealthCheckResult
{
    /// <summary>
    /// Overall health status of the plugin.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// Human-readable message describing the health state.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Time taken to perform the health check in milliseconds.
    /// </summary>
    public double CheckDurationMs { get; init; }

    /// <summary>
    /// When the health check was performed.
    /// </summary>
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Individual component health statuses, if applicable.
    /// </summary>
    public IReadOnlyDictionary<string, ComponentHealth> Components { get; init; } = new Dictionary<string, ComponentHealth>(StringComparer.Ordinal);

    /// <summary>
    /// Additional diagnostic data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthCheckResult Healthy(string? message = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            Message = message ?? "Plugin is healthy"
        };
    }

    /// <summary>
    /// Creates a degraded result.
    /// </summary>
    public static HealthCheckResult Degraded(string message)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Degraded,
            Message = message
        };
    }

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthCheckResult Unhealthy(string message)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Unhealthy,
            Message = message
        };
    }

    /// <summary>
    /// Creates an unknown status result.
    /// </summary>
    public static HealthCheckResult Unknown(string? message = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Unknown,
            Message = message ?? "Health status could not be determined"
        };
    }
}

/// <summary>
/// Health status of an individual component within a plugin.
/// </summary>
public sealed record ComponentHealth
{
    /// <summary>
    /// Component health status.
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// Component-specific message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Response time for this component in milliseconds.
    /// </summary>
    public double? ResponseTimeMs { get; init; }
}
