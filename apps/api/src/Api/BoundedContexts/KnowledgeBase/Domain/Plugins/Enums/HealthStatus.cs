// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;

/// <summary>
/// Represents the health status of a plugin.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Plugin is healthy and fully operational.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Plugin is operational but experiencing degraded performance or partial issues.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Plugin is not operational and cannot process requests.
    /// </summary>
    Unhealthy = 2,

    /// <summary>
    /// Plugin health status is unknown or cannot be determined.
    /// </summary>
    Unknown = 3
}
