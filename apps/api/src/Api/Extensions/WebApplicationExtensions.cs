using System.Security.Claims;
using System.Diagnostics;
using Api.Middleware;
using Scalar.AspNetCore;
using Serilog;

namespace Api.Extensions;

internal static class WebApplicationExtensions
{
    public static WebApplication ConfigureMiddlewarePipeline(
        this WebApplication app,
        bool forwardedHeadersEnabled)
    {
#pragma warning disable S125 // Sections of code should not be commented out
        // PERF-11: Response Compression DISABLED - causing ERR_CONTENT_DECODING_FAILED
        // app.UseResponseCompression();
#pragma warning restore S125

        ConfigureSecurityMiddleware(app, forwardedHeadersEnabled);
        ConfigureObservabilityMiddleware(app);
        ConfigureAuthMiddleware(app);

        return app;
    }

    private static void ConfigureSecurityMiddleware(WebApplication app, bool forwardedHeadersEnabled)
    {
        // BGAI-081: Cookie Policy (development only - allow SameSite=None without Secure)
        if (app.Environment.IsDevelopment())
        {
            app.UseCookiePolicy();
        }

        // Forwarded headers (if enabled)
        if (forwardedHeadersEnabled)
        {
            app.UseForwardedHeaders();
        }

        // CORS (must be before other middleware to handle preflight requests)
        app.UseCors("web");

        // Issue #1447: Security headers (after CORS to avoid interfering with preflight)
        app.UseSecurityHeaders();
    }

    private static void ConfigureObservabilityMiddleware(WebApplication app)
    {
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

                if (httpContext.User.Identity?.IsAuthenticated is true)
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

        // Issue #1563: Add trace context to response headers for frontend correlation
        app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Append("X-Trace-Id", activity.TraceId.ToString());
                    context.Response.Headers.Append("X-Span-Id", activity.SpanId.ToString());
                    return Task.CompletedTask;
                });
            }

            await next().ConfigureAwait(false);
        });
    }

    private static void ConfigureAuthMiddleware(WebApplication app)
    {
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

        // ISSUE #2424: Rate limiting middleware (must be after authorization)
        app.UseRateLimiter();

        // ISSUE #3671: Session quota enforcement middleware (must be after rate limiting)
        app.UseSessionQuotaEnforcement();
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
                // Issue #2755: Add W3C Trace Context headers (traceparent, tracestate) for OpenTelemetry
                policy
                    .WithHeaders(
                        "Content-Type",
                        "Authorization",
                        "X-Correlation-ID",
                        "X-API-Key",
                        "traceparent",  // W3C Trace Context propagation
                        "tracestate"    // W3C Trace Context state
                    )
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Trace-Id", "X-Span-Id", "traceparent", "tracestate");
            });
        });

        return services;
    }
}
