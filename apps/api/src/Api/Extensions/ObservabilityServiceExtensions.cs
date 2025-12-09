using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Api.Observability;
using Api.Infrastructure;
using Api.Infrastructure.Telemetry;
using Microsoft.OpenApi.Models;
using System.Diagnostics;

namespace Api.Extensions;

public static class ObservabilityServiceExtensions
{
    public static IServiceCollection AddObservabilityServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOpenTelemetryServices(configuration, environment);
        services.AddHealthCheckServices(configuration, environment);
        services.AddSwaggerServices();

        return services;
    }

    private static IServiceCollection AddOpenTelemetryServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Issue #1563: HyperDX OpenTelemetry configuration
        var hyperDxEndpoint = configuration["HYPERDX_OTLP_ENDPOINT"] ?? "http://meepleai-hyperdx:14317";
        var serviceName = "meepleai-api";
        var serviceVersion = "1.0.0";

        // Determine batch sizes based on environment
        var isDevelopment = environment.IsDevelopment();
        var maxQueueSize = isDevelopment ? 512 : 2048;
        var maxExportBatchSize = isDevelopment ? 128 : 512;

        // Issue #1567: Add logging for OpenTelemetry debugging
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddFilter("OpenTelemetry", LogLevel.Debug);
            logging.AddFilter("System.Net.Http", LogLevel.Debug);
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName,
                    ["service.namespace"] = "meepleai"
                }))
            .WithTracing(tracing => tracing
                .SetSampler(new AlwaysOnSampler())
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                    {
                        // Don't trace health checks or metrics endpoints
                        var path = httpContext.Request.Path.Value ?? string.Empty;
                        return !path.StartsWith("/health") && !path.Equals("/metrics");
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation()
                // Add explicit Activity Sources for tracing (not Meter sources)
                .AddSource("Microsoft.AspNetCore")  // ASP.NET Core framework traces
                .AddSource("System.Net.Http")       // HTTP client traces
                .AddSource("test-telemetry")        // Issue #1567: Manual test spans
                                                    // Add custom MeepleAI Activity Sources for domain-specific tracing
                .AddSource(MeepleAiActivitySources.ApiSourceName)
                .AddSource(MeepleAiActivitySources.RagSourceName)
                .AddSource(MeepleAiActivitySources.VectorSearchSourceName)
                .AddSource(MeepleAiActivitySources.PdfProcessingSourceName)
                .AddSource(MeepleAiActivitySources.CacheSourceName)
                // Issue #1563: Add sensitive data processor (P1-SEC3)
                .AddProcessor(new SensitiveDataProcessor())
                // Issue #1567: HyperDX OTLP Exporter (HTTP instead of gRPC for compatibility)
                // gRPC in .NET has issues with insecure connections even with AppContext switch
                // Using HTTP/Protobuf as more reliable alternative for local development
                .AddOtlpExporter(options =>
                {
                    // Use HTTP endpoint (4318) instead of gRPC (4317)
                    // HTTP/Protobuf requires explicit /v1/traces path for traces
                    var httpEndpoint = hyperDxEndpoint.Replace(":4317", ":4318");
                    if (!httpEndpoint.EndsWith("/v1/traces"))
                    {
                        httpEndpoint = httpEndpoint.TrimEnd('/') + "/v1/traces";
                    }
                    options.Endpoint = new Uri(httpEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    options.ExportProcessorType = OpenTelemetry.ExportProcessorType.Batch;
                    options.BatchExportProcessorOptions = new OpenTelemetry.BatchExportProcessorOptions<Activity>
                    {
                        MaxQueueSize = maxQueueSize,
                        ScheduledDelayMilliseconds = 5000,
                        ExporterTimeoutMilliseconds = 30000,
                        MaxExportBatchSize = maxExportBatchSize
                    };
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MeepleAiMetrics.MeterName)
                // Keep Prometheus exporter for Grafana infrastructure metrics
                .AddPrometheusExporter()
                // Issue #1567: Add HyperDX OTLP Exporter for metrics (HTTP for compatibility)
                // Note: Metrics use PeriodicExportingMetricReader, not BatchProcessor like traces
                .AddOtlpExporter(options =>
                {
                    // HTTP/Protobuf requires explicit /v1/metrics path for metrics
                    var httpEndpoint = hyperDxEndpoint.Replace(":4317", ":4318");
                    if (!httpEndpoint.EndsWith("/v1/metrics"))
                    {
                        httpEndpoint = httpEndpoint.TrimEnd('/') + "/v1/metrics";
                    }
                    options.Endpoint = new Uri(httpEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                }));

        return services;
    }

    /// <summary>
    /// Adds health check services for database, cache, and vector store monitoring.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The web host environment.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the database connection string is not configured via
    /// ConnectionStrings:Postgres in appsettings.json or CONNECTIONSTRINGS__POSTGRES environment variable,
    /// except in Testing environment where health checks are skipped.
    /// </exception>
    private static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // OPS-01: Health checks for observability
        var healthCheckRedisConnectionString = configuration["REDIS_URL"] ?? "localhost:6379";
        var healthCheckQdrantUrl = configuration["QDRANT_URL"] ?? "http://localhost:6333";

        var healthChecksBuilder = services.AddHealthChecks();

        // Skip Postgres health check in Testing environment (uses SQLite in-memory DB)
        if (!environment.IsEnvironment("Testing"))
        {
            // SEC-708: Build connection string from Docker Secrets if available (only for non-testing)
            var healthCheckConnectionString = configuration.GetConnectionString("Postgres")
                ?? configuration["ConnectionStrings__Postgres"]
                ?? SecretsHelper.BuildPostgresConnectionString(configuration);

            if (string.IsNullOrEmpty(healthCheckConnectionString))
            {
                throw new InvalidOperationException(
                    "Health check database connection string not configured. " +
                    "Set CONNECTIONSTRINGS__POSTGRES environment variable or appsettings.json ConnectionStrings:Postgres.");
            }

            healthChecksBuilder.AddNpgSql(
                healthCheckConnectionString,
                name: "postgres",
                tags: new[] { "db", "sql" });
        }

        healthChecksBuilder
            .AddRedis(
                healthCheckRedisConnectionString,
                name: "redis",
                tags: new[] { "cache", "redis" })
            .AddUrlGroup(
                new Uri($"{healthCheckQdrantUrl}/healthz"),
                name: "qdrant",
                tags: new[] { "vector", "qdrant" })
            .AddCheck<QdrantHealthCheck>(
                "qdrant-collection",
                tags: new[] { "vector", "qdrant", "collection" });

        // Issue #892: Additional health checks for n8n and HyperDX
        var n8nUrl = configuration["N8N_URL"] ?? "http://n8n:5678";
        var hyperDxUrl = configuration["HYPERDX_URL"] ?? "http://meepleai-hyperdx:8000";

        healthChecksBuilder
            .AddUrlGroup(
                new Uri($"{n8nUrl}/healthz"),
                name: "n8n",
                tags: new[] { "automation", "workflow" })
            .AddUrlGroup(
                new Uri($"{hyperDxUrl}/health"),
                name: "hyperdx",
                tags: new[] { "observability", "monitoring" });

        return services;
    }

    /// <summary>
    /// Configures Swagger/OpenAPI documentation with dual authentication support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Registers two security schemes:
    /// <list type="bullet">
    /// <item><description><b>ApiKey</b>: Authorization header-based authentication (Authorization: ApiKey {key})</description></item>
    /// <item><description><b>Cookie</b>: Session cookie-based authentication (meepleai_session)</description></item>
    /// </list>
    /// Both schemes are applied globally, allowing clients to use either method.
    /// </remarks>
    private static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        // API-01: Native .NET 9 OpenAPI configuration
        services.AddOpenApi();

        return services;
    }
}
