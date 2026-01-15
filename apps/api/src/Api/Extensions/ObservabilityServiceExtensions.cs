using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Api.Observability;
using Api.Infrastructure;
using Api.Infrastructure.Telemetry;
using Microsoft.OpenApi.Models;
using System.Diagnostics;

namespace Api.Extensions;

internal static class ObservabilityServiceExtensions
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

    private static readonly string[] PostgresTags = new[] { "db", "sql" };
    private static readonly string[] RedisTags = new[] { "cache", "redis" };
    private static readonly string[] QdrantTags = new[] { "vector", "qdrant" };
    private static readonly string[] QdrantCollectionTags = new[] { "vector", "qdrant", "collection" };
    private static readonly string[] N8nTags = new[] { "automation", "workflow" };
    private static readonly string[] SharedCatalogTags = new[] { "database", "fts", "shared-catalog" };
    private static readonly string[] ConfigurationTags = new[] { "configuration", "startup" };

    private static IServiceCollection AddOpenTelemetryServices(
            this IServiceCollection services,
            IConfiguration configuration,
            IWebHostEnvironment environment)
    {
        // Issue #1563: HyperDX OpenTelemetry configuration
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        var hyperDxEndpoint = configuration["HYPERDX_OTLP_ENDPOINT"] ?? "http://meepleai-hyperdx:14317";
#pragma warning restore S1075
        var serviceName = "meepleai-api";
        var serviceVersion = "1.0.0";
        var isDevelopment = environment.IsDevelopment();

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
                .AddAttributes(new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["deployment.environment"] = environment.EnvironmentName,
                    ["service.namespace"] = "meepleai"
                }))
            .WithTracing(tracing => tracing.ConfigureTracing(hyperDxEndpoint, isDevelopment))
            .WithMetrics(metrics => metrics.ConfigureMetrics(hyperDxEndpoint));

        return services;
    }

    private static TracerProviderBuilder ConfigureTracing(this TracerProviderBuilder tracing, string hyperDxEndpoint, bool isDevelopment)
    {
        var maxQueueSize = isDevelopment ? 512 : 2048;
        var maxExportBatchSize = isDevelopment ? 128 : 512;

        return tracing
            .SetSampler(new AlwaysOnSampler())
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                {
                    // Don't trace health checks or metrics endpoints
                    var path = httpContext.Request.Path.Value ?? string.Empty;
                    return !path.StartsWith("/health", StringComparison.Ordinal) && !path.Equals("/metrics", StringComparison.Ordinal);
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
#pragma warning disable CA2000 // Dispose objects before losing scope - OpenTelemetry takes ownership
            .AddProcessor(new SensitiveDataProcessor())
#pragma warning restore CA2000
            // Issue #1567: HyperDX OTLP Exporter (HTTP instead of gRPC for compatibility)
            // gRPC in .NET has issues with insecure connections even with AppContext switch
            // Using HTTP/Protobuf as more reliable alternative for local development
            .AddOtlpExporter(options =>
            {
                // Use HTTP endpoint (4318) instead of gRPC (4317)
                // HTTP/Protobuf requires explicit /v1/traces path for traces
                var httpEndpoint = hyperDxEndpoint.Replace(":4317", ":4318");
                if (!httpEndpoint.EndsWith("/v1/traces", StringComparison.Ordinal))
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
            });
    }

    private static MeterProviderBuilder ConfigureMetrics(this MeterProviderBuilder metrics, string hyperDxEndpoint)
    {
        return metrics
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
                if (!httpEndpoint.EndsWith("/v1/metrics", StringComparison.Ordinal))
                {
                    httpEndpoint = httpEndpoint.TrimEnd('/') + "/v1/metrics";
                }
                options.Endpoint = new Uri(httpEndpoint);
                options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });
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
        var healthChecksBuilder = services.AddHealthChecks();

        AddPostgresHealthCheck(healthChecksBuilder, configuration, environment);
        AddExternalHealthChecks(healthChecksBuilder, configuration);

        return services;
    }

    private static void AddPostgresHealthCheck(IHealthChecksBuilder builder, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Skip Postgres health check in Testing environment (uses SQLite in-memory DB)
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        // Issue #2152: Allow disabling Postgres health check via configuration
        var healthCheckEnabled = configuration.GetValue("HealthChecks:Postgres:Enabled", defaultValue: true);
        if (!healthCheckEnabled)
        {
            Console.WriteLine("[INFO] PostgreSQL health check disabled via HealthChecks:Postgres:Enabled configuration");
            return;
        }

        // Issue #2152: Try SecretsHelper FIRST (reads uncorrupted POSTGRES_* vars)
        var secretsHelperResult = SecretsHelper.BuildPostgresConnectionString(configuration);

        // SEC-708: Build connection string from Docker Secrets if available (only for non-testing)
        // Issue #2152: Use SecretsHelper if succeeded, otherwise fallback
        var healthCheckConnectionString = secretsHelperResult != null
            ? secretsHelperResult
            : (Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                ?? configuration["ConnectionStrings__Postgres"]
                ?? configuration.GetConnectionString("Postgres"));

        if (string.IsNullOrEmpty(healthCheckConnectionString))
        {
            throw new InvalidOperationException(
                "Health check database connection string not configured. " +
                "Set CONNECTIONSTRINGS__POSTGRES environment variable or appsettings.json ConnectionStrings:Postgres.");
        }

        builder.AddNpgSql(
            healthCheckConnectionString,
            name: "postgres",
            tags: PostgresTags);
    }

    private static void AddExternalHealthChecks(IHealthChecksBuilder builder, IConfiguration configuration)
    {
        // S1075: Default URLs extracted to const
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback values
        const string DefaultRedisConnectionString = "localhost:6379";
        const string DefaultQdrantUrl = "http://localhost:6333";
        const string DefaultN8nUrl = "http://n8n:5678";
#pragma warning restore S1075

        var healthCheckRedisConnectionString = configuration["REDIS_URL"] ?? DefaultRedisConnectionString;
        var healthCheckQdrantUrl = configuration["QDRANT_URL"] ?? DefaultQdrantUrl;
        var n8nUrl = configuration["N8N_URL"] ?? DefaultN8nUrl;

        builder
            .AddRedis(
                healthCheckRedisConnectionString,
                name: "redis",
                tags: RedisTags)
            .AddUrlGroup(
                new Uri($"{healthCheckQdrantUrl}/healthz"),
                name: "qdrant",
                tags: QdrantTags)
            .AddCheck<QdrantHealthCheck>(
                "qdrant-collection",
                tags: QdrantCollectionTags)
            .AddCheck<Api.Infrastructure.HealthChecks.SharedGameCatalogHealthCheck>(
                "shared-catalog-fts",
                failureStatus: HealthStatus.Degraded,
                tags: SharedCatalogTags)
            .AddUrlGroup(
                new Uri($"{n8nUrl}/healthz"),
                name: "n8n",
                tags: N8nTags)
            .AddCheck<Api.Infrastructure.HealthChecks.ConfigurationHealthCheck>(
                "configuration",
                failureStatus: HealthStatus.Degraded,
                tags: ConfigurationTags);
#pragma warning disable S125 // Sections of code should not be commented out
        // HyperDX health check disabled - service not in default docker-compose profile
        // .AddUrlGroup(
        //     new Uri($"{hyperDxUrl}/health"),
        //     name: "hyperdx",
        //     tags: HyperDxTags);
#pragma warning restore S125
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
        // API-01: Native .NET 9 OpenAPI configuration with XML comments
        services.AddOpenApi(options =>
        {
            // Living Documentation: Include XML comments in OpenAPI spec
            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);

            if (File.Exists(xmlPath))
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    // XML comments are automatically included by .NET 9 OpenAPI
                    // No manual parsing needed - just ensure file exists
                    return Task.CompletedTask;
                });
            }
        });

        return services;
    }
}
