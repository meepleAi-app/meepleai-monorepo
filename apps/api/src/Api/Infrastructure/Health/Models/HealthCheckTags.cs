namespace Api.Infrastructure.Health.Models;

/// <summary>
/// Standard tags for categorizing health checks by service category and criticality.
/// </summary>
public static class HealthCheckTags
{
    // Service categories
    /// <summary>
    /// Core infrastructure services (PostgreSQL, Redis, Qdrant).
    /// </summary>
    public const string Core = "core";

    /// <summary>
    /// AI services (OpenRouter, Embedding, Reranker, PDF processing).
    /// </summary>
    public const string Ai = "ai";

    /// <summary>
    /// External APIs (BGG, OAuth providers, Email/SMTP).
    /// </summary>
    public const string External = "external";

    /// <summary>
    /// Monitoring services (Grafana, Prometheus, OpenTelemetry).
    /// </summary>
    public const string Monitoring = "monitoring";

    /// <summary>
    /// Storage services (S3, local filesystem, object storage).
    /// </summary>
    public const string Storage = "storage";

    // Criticality levels
    /// <summary>
    /// Critical service - application cannot function without it.
    /// Failure results in overall status = Unhealthy.
    /// </summary>
    public const string Critical = "critical";

    /// <summary>
    /// Non-critical service - application can operate in degraded mode.
    /// Failure results in overall status = Degraded.
    /// </summary>
    public const string NonCritical = "non-critical";
}
