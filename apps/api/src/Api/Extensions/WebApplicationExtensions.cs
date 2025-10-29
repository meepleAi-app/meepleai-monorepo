using System.Security.Claims;
using Api.Middleware;
using Serilog;

namespace Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigureMiddlewarePipeline(
        this WebApplication app,
        bool forwardedHeadersEnabled)
    {
        // PERF-11: Enable Response Compression (must be early in pipeline)
        app.UseResponseCompression();

        // Forwarded headers (if enabled)
        if (forwardedHeadersEnabled)
        {
            app.UseForwardedHeaders();
        }

        // CORS
        app.UseCors("web");

        // OPS-02: OpenTelemetry Prometheus metrics endpoint
        app.MapPrometheusScrapingEndpoint();

        // API-01: Swagger UI (development only)
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "MeepleAI API v1");
                options.RoutePrefix = "api/docs"; // Swagger UI at /api/docs
                options.DocumentTitle = "MeepleAI API Documentation";
            });
        }

        // API-01: API exception handler middleware (must be early in pipeline)
        app.UseApiExceptionHandler();

        // Request logging with correlation ID
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
                // Provide a CorrelationId property to align with logging tests and tooling
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
                diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
                diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("RemoteIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
                    diagnosticContext.Set("UserEmail", httpContext.User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown");
                }
            };
        });

        // Add correlation ID to response headers
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                // Use canonical header casing expected by clients and tests
                context.Response.Headers.Append("X-Correlation-Id", context.TraceIdentifier);
                return Task.CompletedTask;
            });

            await next();
        });

        // API-01: API key authentication middleware (must be before authorization)
        app.UseApiKeyAuthentication();

        // Note: Additional authentication, authorization, and rate limiting middleware
        // should be configured after calling this method but before MapEndpoints.

        return app;
    }

    public static IServiceCollection AddCorsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("web", policy =>
            {
                var corsOrigins = configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                var topLevelOrigins = configuration
                    .GetSection("AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                var configuredOrigins = corsOrigins
                    .Concat(topLevelOrigins)
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (configuredOrigins.Length == 0)
                {
                    policy.WithOrigins("http://localhost:3000");
                }
                else
                {
                    policy.WithOrigins(configuredOrigins);
                }

                policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            });
        });

        return services;
    }
}
