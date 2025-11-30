using System.Security.Claims;
using Api.Middleware;
using Scalar.AspNetCore;
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

        // Issue #1447: Security headers (must be before CORS to ensure headers on all responses including preflight)
        app.UseSecurityHeaders();

        // CORS
        app.UseCors("web");

        // OPS-02: OpenTelemetry Prometheus metrics endpoint
        app.MapPrometheusScrapingEndpoint();

        // API-01: Native .NET 9 OpenAPI with Scalar UI (development only)
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();

            // Issue #1543: Scalar - Modern OpenAPI documentation UI
            // Access at /scalar/v1
            app.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("MeepleAI API")
                    .WithTheme(Scalar.AspNetCore.ScalarTheme.DeepSpace)
                    .WithDefaultHttpClient(Scalar.AspNetCore.ScalarTarget.CSharp, Scalar.AspNetCore.ScalarClient.HttpClient);
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

            await next().ConfigureAwait(false);
        });

        // AUTH-03: Session cookie authentication (must be before API key and authorization)
        // This middleware reads session cookies and populates HttpContext.Items["ActiveSession"]
        app.UseSessionAuthentication();

        // AUTH-03: Standard authentication middleware for ClaimsPrincipal
        app.UseAuthentication();

        // API-01: API key authentication middleware (must be after UseAuthentication)
        app.UseApiKeyAuthentication();

        // Enforce hourly/daily quota on API keys once the principal has been set
        app.UseMiddleware<ApiKeyQuotaEnforcementMiddleware>();

        // AUTH-03: Authorization middleware (must be after all authentication middleware)
        app.UseAuthorization();

        // Rate limiting middleware (must be after authorization to read user role from ActiveSession)
        app.UseRoleAwareRateLimiting();

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

                // Issue #1448: Whitelist specific headers instead of AllowAnyHeader() for security
                policy
                    .WithHeaders(
                        "Content-Type",
                        "Authorization",
                        "X-Correlation-ID",
                        "X-API-Key"
                    )
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
