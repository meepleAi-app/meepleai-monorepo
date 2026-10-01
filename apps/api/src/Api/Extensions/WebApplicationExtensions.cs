using System.Security.Claims;
using System.Diagnostics;
using Api.BoundedContexts.Authentication.Application.Services;
using Api.BoundedContexts.Authentication.Infrastructure.Middleware;
using Api.Middleware;
using Microsoft.Extensions.DependencyInjection;
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

        // Enable request body buffering so the body stream can be re-read.
        // Required because .NET 9 middleware may consume the body stream before
        // endpoint parameter binding, causing InvalidJsonRequestBody errors.
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            await next().ConfigureAwait(false);
        });

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

    /// <summary>
    /// Reads a boolean switch, treating an empty or whitespace-only value as absent.
    /// <para>
    /// ISSUE #3998 — the defect this exists for: <c>IConfiguration.GetValue&lt;bool&gt;(key, default)</c>
    /// falls back to the default only when the key is MISSING. When the key is present and holds the
    /// empty string it converts it, and <c>BooleanConverter</c> → <c>bool.Parse("")</c> throws
    /// <c>InvalidOperationException</c>. Docker Compose makes that case the norm rather than the
    /// exception: <c>infra/compose.dev.yml</c> passes
    /// <c>DISABLE_RATE_LIMITING: ${DISABLE_RATE_LIMITING:-}</c>, and <c>${VAR:-}</c> expands to the
    /// empty string whenever VAR is not in the host environment. So an unset optional switch reaches
    /// the container as a present-but-empty key.
    /// </para>
    /// <para>
    /// The throw happened during <c>ConfigureMiddlewarePipeline</c>, before Kestrel started
    /// listening, so the process died with no HTTP surface and nothing in the logs — the snapshot
    /// bake was red for five weeks because of it. Measured breadth at the time of the fix: of the
    /// variables the compose files pass as <c>${X:-}</c>, this is the only one read through a typed
    /// conversion; the others are strings, where empty is harmless. To re-measure:
    /// <c>grep -hoE '\$\{[A-Za-z_][A-Za-z0-9_]*:-\}' infra/compose*.yml | sed -E 's/^\$\{//; s/:-\}$//' | sort -u</c>
    /// </para>
    /// Non-empty values keep going through <c>GetValue&lt;bool&gt;</c> unchanged, so a misspelt value
    /// still throws instead of silently becoming the default.
    /// </summary>
    /// <remarks>
    /// <c>internal</c> and not <c>private</c> so the regression test can exercise it directly:
    /// reproducing the defect through the middleware pipeline would need a full host, while the
    /// behaviour that matters is a three-way decision on one string.
    /// </remarks>
    internal static bool ReadFlag(IConfiguration configuration, string key, bool defaultValue) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? defaultValue
            : configuration.GetValue<bool>(key);

    private static void ConfigureAuthMiddleware(WebApplication app)
    {
        // AUTH-03: Session cookie authentication (must be before API key and authorization)
        // This middleware reads session cookies and populates HttpContext.Items["ActiveSession"]
        app.UseSessionAuthentication();

        // AUTH-03: Standard authentication middleware for ClaimsPrincipal
        app.UseAuthentication();

        // AUTH-03: Authorization middleware (must be after all authentication middleware)
        app.UseAuthorization();

        // C8: antiforgery middleware. Must come after auth so token validation
        // runs against an already-authenticated request, but before rate
        // limiting so a missing CSRF token short-circuits with 400 without
        // burning the rate-limit budget. AntiforgeryEndpointFilter actually
        // performs the per-endpoint validation; UseAntiforgery wires the
        // cookie/token plumbing.
        app.UseAntiforgery();

        // ISSUE #2424: Rate limiting middleware (must be after authorization)
        //
        // ISSUE #3887: the on/off decision lives here, and ONLY here. AddRateLimitingServices
        // registers the real policies unconditionally and reads no switch at all. Service
        // registration runs before a WebApplicationFactory applies its configuration sources, so a
        // test host could only be told "no rate limiting" through a process environment variable —
        // global state that leaked into every host built concurrently by another xUnit collection
        // and made unrelated tests fail with 429. app.Configuration is the post-Build, per-host
        // configuration and still includes the environment-variables source, so a
        // DISABLE_RATE_LIMITING env var keeps working exactly as before in production.
        // This matters now that it is the ONLY place either flag is honoured: a misspelt value used
        // to degrade gracefully because registration produced permissive policies as well.
        //
        // ISSUE #3998: both switches go through ReadFlag, because a variable set to the EMPTY
        // STRING is not the same thing as an absent one — and `GetValue<bool>` treats it as a
        // value, not as "use the default". That is what kept the snapshot bake red for five weeks.
        //
        // Correction to an earlier note here, which claimed that `"1"/"True"/"yes" behave alike`:
        // they do not. `GetValue<bool>` goes through `BooleanConverter` → `bool.Parse`, which
        // accepts only `true`/`false` (case-insensitive). `"1"` and `"yes"` throw, exactly like
        // `""` did. ReadFlag deliberately keeps that strictness for non-empty values: a typo must
        // stay loud, per the paragraph above.
        var rateLimitingEnabled =
            ReadFlag(app.Configuration, "RateLimiting:Enabled", defaultValue: true)
            && !ReadFlag(app.Configuration, "DISABLE_RATE_LIMITING", defaultValue: false);

        if (rateLimitingEnabled)
        {
            app.UseRateLimiter();
        }

        // ISSUE #4275: BGG API tier-based rate limiting (must be after rate limiter)
        app.UseBggRateLimit();

        // ISSUE #3671: Session quota enforcement middleware (must be after rate limiting)
        app.UseSessionQuotaEnforcement();

        // ISSUE #3672: Email verification enforcement middleware (must be after session quota)
        app.UseEmailVerificationEnforcement();

        // DevOps wave 1 (2026-05-08): staging email allowlist gate.
        // Active only when ASPNETCORE_ENVIRONMENT=Staging to match compose.staging.yml.
        // Empty allowlist = pass-through (default safe). Logs warning at startup if
        // Staging+empty (misconfiguration window detection). See devops-policy.md §4.
        if (app.Environment.IsEnvironment("Staging"))
        {
            // Resolve singleton directly from root container — no scope needed since
            // IStagingAccessGuard is registered as Singleton with a 60s memory cache (#845).
            // The hot-path uses a scope factory for DB access; startup probe stays lightweight.
            // Empty allowlist now FAIL-CLOSED in Staging (denies all) — the previous
            // "empty = pass-through" semantics were a latent landmine if the bootstrap
            // seed failed silently.
            var guard = app.Services.GetRequiredService<IStagingAccessGuard>();
            var hasEntries = guard.HasNonEmptyAllowlistAsync().AsTask().GetAwaiter().GetResult();
            if (!hasEntries)
            {
                app.Logger.LogWarning(
                    "STAGING_ACCESS: middleware active but staging_allowlist is empty — " +
                    "all authenticated requests will be denied (fail-closed). " +
                    "Bootstrap seed should have inserted badsworm@gmail.com; " +
                    "check SeedStagingAllowlistCommand execution.");
            }
            app.UseStagingAccessGuard();
        }

        // Reset body stream position for [FromBody] parameter binding.
        // .NET 9 issue: the body stream position may advance during the middleware pipeline
        // even though no middleware explicitly reads it. Without this reset, [FromBody]
        // deserialization fails with "The input does not contain any JSON tokens".
        // Must be the LAST middleware so it runs right before endpoint parameter binding.
        app.Use(async (context, next) =>
        {
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }

            await next().ConfigureAwait(false);
        });
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
                    // #3146 / Invariante 4: expose the non-blocking save-warning headers so the
                    // cross-origin FE can read them (mirrors the Program.cs "web" policy).
                    .WithExposedHeaders("X-Trace-Id", "X-Span-Id", "traceparent", "tracestate", "X-Warning-Code", "X-Live-Session-Id");
            });
        });

        return services;
    }
}
