using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Api.Observability;
using Api.Infrastructure;
using Api.Infrastructure.Telemetry;
using Api.Infrastructure.Health.Extensions;
using Api.Infrastructure.Health.Models;

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

    private static readonly string[] PostgresTags = new[] { "db", "sql", HealthCheckTags.Core, HealthCheckTags.Critical };
    private static readonly string[] RedisTags = new[] { "cache", "redis", HealthCheckTags.Core, HealthCheckTags.Critical };
    // Qdrant health check tags removed — pgvector is now the sole vector store (no separate health check needed)
    private static readonly string[] N8nTags = new[] { "automation", "workflow" };
    private static readonly string[] SharedCatalogTags = new[] { "database", "fts", "shared-catalog" };
    private static readonly string[] ConfigurationTags = new[] { "configuration", "startup" };

    private static IServiceCollection AddOpenTelemetryServices(
            this IServiceCollection services,
            IConfiguration configuration,
            IWebHostEnvironment environment)
    {
        var serviceName = "meepleai-api";
        var serviceVersion = "1.0.0";

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
            .WithTracing(tracing => tracing.ConfigureTracing())
            .WithMetrics(metrics => metrics.ConfigureMetrics());

        return services;
    }

    private static TracerProviderBuilder ConfigureTracing(this TracerProviderBuilder tracing)
    {
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
                                                // Add custom MeepleAI Activity Sources for domain-specific tracing
            .AddSource(MeepleAiActivitySources.ApiSourceName)
            .AddSource(MeepleAiActivitySources.RagSourceName)
            .AddSource(MeepleAiActivitySources.VectorSearchSourceName)
            .AddSource(MeepleAiActivitySources.PdfProcessingSourceName)
            .AddSource(MeepleAiActivitySources.CacheSourceName)
            // Issue #1563: Sensitive data processor (P1-SEC3)
#pragma warning disable CA2000 // Dispose objects before losing scope - OpenTelemetry takes ownership
            .AddProcessor(new SensitiveDataProcessor());
#pragma warning restore CA2000
    }

    private static MeterProviderBuilder ConfigureMetrics(this MeterProviderBuilder metrics)
    {
        return metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(MeepleAiMetrics.MeterName)
            // Prometheus exporter for Grafana infrastructure metrics
            .AddPrometheusExporter();
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

        // Add comprehensive health checks for AI, External APIs, and Monitoring
        healthChecksBuilder.AddComprehensiveHealthChecks();

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

        // SEC-708: Explicit connection string takes highest priority (allows SSL Mode, extra params)
        var envVarConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");

        // Issue #2152: Try SecretsHelper (reads POSTGRES_* vars) as fallback
        var secretsHelperResult = SecretsHelper.BuildPostgresConnectionString(configuration);

        var healthCheckConnectionString = envVarConnectionString
            ?? secretsHelperResult
            ?? configuration["ConnectionStrings__Postgres"]
            ?? configuration.GetConnectionString("Postgres");

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
        const string DefaultN8nUrl = "http://n8n:5678";
#pragma warning restore S1075

        // Issue #2152: Build Redis connection string with password (same as InfrastructureServiceExtensions)
        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST")
            ?? configuration["REDIS_HOST"]
            ?? "localhost";
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT")
            ?? configuration["REDIS_PORT"]
            ?? "6379";
        var redisPassword = SecretsHelper.GetSecretOrValue(configuration, "REDIS_PASSWORD", logger: null, required: false)
            ?? Environment.GetEnvironmentVariable("REDIS_PASSWORD");

        var healthCheckRedisConnectionString = string.IsNullOrWhiteSpace(redisPassword)
            ? $"{redisHost}:{redisPort}"
            : $"{redisHost}:{redisPort},password={redisPassword}";

        var n8nUrl = configuration["N8N_URL"] ?? DefaultN8nUrl;

        builder
            .AddRedis(
                healthCheckRedisConnectionString,
                name: "redis",
                tags: RedisTags)
            // Qdrant health checks removed — pgvector is the sole vector store (health covered by Postgres check)
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
