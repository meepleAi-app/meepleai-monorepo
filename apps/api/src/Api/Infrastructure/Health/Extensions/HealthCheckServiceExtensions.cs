using Api.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;
using Api.Infrastructure.Health.Checks;
using Api.Infrastructure.Health.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Extensions;

/// <summary>
/// Extension methods for registering all comprehensive health checks.
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    /// Registers all comprehensive health checks for Core, AI, External, and Monitoring services.
    /// </summary>
    /// <param name="builder">Health checks builder</param>
    /// <returns>The builder for chaining</returns>
    public static IHealthChecksBuilder AddComprehensiveHealthChecks(this IHealthChecksBuilder builder)
    {
        // Core Infrastructure (Critical)
        // PostgreSQL, Redis already registered in ObservabilityServiceExtensions
        // with tags: "db", "cache" - we add critical tag in ObservabilityServiceExtensions
        // Note: Qdrant removed — pgvector is the sole vector store (covered by Postgres health check)

        // AI Services
        builder.AddCheck<OpenRouterHealthCheck>(
            "openrouter",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Embedding is essential for RAG but not for app startup — treat as Degraded
        // to avoid blocking CI health checks and startup without embedding service
        builder.AddCheck<EmbeddingServiceHealthCheck>(
            "embedding",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<RerankerHealthCheck>(
            "reranker",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<UnstructuredHealthCheck>(
            "unstructured",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<SmolDoclingHealthCheck>(
            "smoldocling",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // External APIs
        builder.AddCheck<BggApiHealthCheck>(
            "bggapi",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.External, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<OAuthProvidersHealthCheck>(
            "oauth",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.External, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<EmailSmtpHealthCheck>(
            "smtp",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.External, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Monitoring Services
        builder.AddCheck<GrafanaHealthCheck>(
            "grafana",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Monitoring, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<PrometheusHealthCheck>(
            "prometheus",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Monitoring, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<HyperDxHealthCheck>(
            "hyperdx",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Monitoring, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Issue #5477: Redis rate-limiting subsystem health
        builder.AddCheck<RedisRateLimitingHealthCheck>(
            "redis-rate-limiting",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Core, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Storage Services
        builder.AddCheck<S3StorageHealthCheck>(
            "s3storage",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Storage, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Slack Notification Services
        builder.AddCheck<SlackApiHealthCheck>(
            "slack_api",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.External, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        builder.AddCheck<SlackQueueHealthCheck>(
            "slack_queue",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.External, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        return builder;
    }
}
