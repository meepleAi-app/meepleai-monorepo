using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Api.Observability;
using Api.Infrastructure;

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
        // OPS-02: OpenTelemetry configuration
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4318";
        var serviceName = "MeepleAI.Api";
        var serviceVersion = "1.0.0";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName))
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
                // Add explicit Activity Sources for tracing (not Meter sources)
                .AddSource("Microsoft.AspNetCore")  // ASP.NET Core framework traces
                .AddSource("System.Net.Http")       // HTTP client traces
                // Add custom MeepleAI Activity Sources for domain-specific tracing
                .AddSource(MeepleAiActivitySources.ApiSourceName)
                .AddSource(MeepleAiActivitySources.RagSourceName)
                .AddSource(MeepleAiActivitySources.VectorSearchSourceName)
                .AddSource(MeepleAiActivitySources.PdfProcessingSourceName)
                .AddSource(MeepleAiActivitySources.CacheSourceName)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MeepleAiMetrics.MeterName)
                .AddPrometheusExporter());

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
        var healthCheckConnectionString = configuration.GetConnectionString("Postgres")
            ?? configuration["ConnectionStrings__Postgres"];

        var healthCheckRedisConnectionString = configuration["REDIS_URL"] ?? "localhost:6379";
        var healthCheckQdrantUrl = configuration["QDRANT_URL"] ?? "http://localhost:6333";

        var healthChecksBuilder = services.AddHealthChecks();

        // Skip Postgres health check in Testing environment (uses SQLite in-memory DB)
        if (!environment.IsEnvironment("Testing"))
        {
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

        return services;
    }

    private static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        // API-01: OpenAPI/Swagger configuration
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Version = "v1",
                Title = "MeepleAI API",
                Description = "AI-powered board game rules assistant API with RAG-based question answering, rule explanations, and chess analysis",
                Contact = new Microsoft.OpenApi.Models.OpenApiContact
                {
                    Name = "MeepleAI Support",
                    Email = "support@meepleai.dev"
                }
            });

            // API Key Security Scheme
            options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "API key authentication. Format: mpl_live_{40_random_chars} or mpl_test_{40_random_chars}"
            });

            // Cookie Security Scheme (existing session-based auth)
            options.AddSecurityDefinition("Cookie", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
                Name = "meeple_session",
                Description = "Cookie-based session authentication for web clients"
            });

            // Apply security requirements globally
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        }
                    },
                    Array.Empty<string>()
                },
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Cookie"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
