using Api.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;
using Api.Infrastructure.Health.Checks;
using Api.Infrastructure.Health.Models;
using Api.Services;
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
    /// <param name="configuration">Application configuration used to skip registration of optional providers that are not selected</param>
    /// <returns>The builder for chaining</returns>
    /// <remarks>
    /// Optional providers (Unstructured, SmolDocling, Ollama) are registered conditionally based on
    /// runtime configuration. When a provider is not the active selection, its health check is not
    /// registered at all — this prevents the global /health endpoint from reporting Degraded for
    /// services that are intentionally not deployed (avoiding alert fatigue and SLA noise).
    /// </remarks>
    public static IHealthChecksBuilder AddComprehensiveHealthChecks(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
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

        // Embedding dimension validation — catches provider/schema mismatch at startup.
        // Uses factory registration because IEmbeddingService is internal (CS0051 prevents
        // a public constructor), and ActivatorUtilities does not resolve internal constructors.
        builder.Add(new HealthCheckRegistration(
            "embedding-dimensions",
            sp => new EmbeddingDimensionHealthCheck(
                sp.GetRequiredService<IEmbeddingService>(),
                sp.GetRequiredService<ILogger<EmbeddingDimensionHealthCheck>>()),
            HealthStatus.Degraded,
            new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            TimeSpan.FromSeconds(1)));

        builder.AddCheck<RerankerHealthCheck>(
            "reranker",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Conditional: PDF extractor providers — only register the check when the
        // provider is actually in use. The "Orchestrator" provider routes to both
        // Unstructured and SmolDocling; explicit single-provider settings register
        // only that one. Other values (e.g. "Docnet") register neither.
        // Null/empty/whitespace falls back to "Orchestrator" to keep dev defaults
        // working when the key is absent or blank in configuration.
        var pdfProviderRaw = configuration["PdfProcessing:Extractor:Provider"];
        var pdfProvider = string.IsNullOrWhiteSpace(pdfProviderRaw) ? "Orchestrator" : pdfProviderRaw;
        var registerUnstructured = pdfProvider.Equals("Orchestrator", StringComparison.OrdinalIgnoreCase) ||
                                   pdfProvider.Equals("Unstructured", StringComparison.OrdinalIgnoreCase);
        var registerSmolDocling = pdfProvider.Equals("Orchestrator", StringComparison.OrdinalIgnoreCase) ||
                                  pdfProvider.Equals("SmolDocling", StringComparison.OrdinalIgnoreCase);

        if (registerUnstructured)
        {
            builder.AddCheck<UnstructuredHealthCheck>(
                "unstructured",
                HealthStatus.Degraded,
                tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical, HealthCheckTags.Optional },
                timeout: TimeSpan.FromSeconds(5));
        }

        if (registerSmolDocling)
        {
            builder.AddCheck<SmolDoclingHealthCheck>(
                "smoldocling",
                HealthStatus.Degraded,
                tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical, HealthCheckTags.Optional },
                timeout: TimeSpan.FromSeconds(5));
        }

        builder.AddCheck<OrchestrationHealthCheck>(
            "orchestrator",
            HealthStatus.Degraded,
            tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical },
            timeout: TimeSpan.FromSeconds(5));

        // Conditional: Ollama is opt-in — register only when OLLAMA_URL is set.
        var ollamaUrl = configuration["OLLAMA_URL"];
        if (!string.IsNullOrWhiteSpace(ollamaUrl))
        {
            builder.AddCheck<OllamaHealthCheck>(
                "ollama",
                HealthStatus.Degraded,
                tags: new[] { HealthCheckTags.Ai, HealthCheckTags.NonCritical, HealthCheckTags.Optional },
                timeout: TimeSpan.FromSeconds(5));
        }

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
