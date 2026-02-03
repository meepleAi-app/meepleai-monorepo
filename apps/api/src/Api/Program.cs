using DotNetEnv;
using Api.Configuration; // CHAT-02
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Logging;
using Api.Middleware;
using Api.Models;
using Api.Observability;
using Api.Routing;
using Api.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc; // EDIT-05: For [FromServices] attribute
using Microsoft.AspNetCore.ResponseCompression; // PERF-11: Response compression
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly; // AI-13: Retry policies for BGG API
using Polly.Extensions.Http; // AI-13: HTTP-specific retry policies
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using System;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Claims;
using AspNetIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

// Issue #1567: Enable HTTP/2 for gRPC without TLS (required for OpenTelemetry OTLP exporter)
// This allows gRPC connections over http:// (insecure) instead of requiring https://
// Required for local development with HyperDX
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Load environment variables from .env.development file (single source of truth for secrets)
// SECURITY: Only load .env files in Development environment - NEVER in Production/CI
// Uses clobberExistingVars: false so launchSettings.json values (localhost) take priority
// This allows: OAuth credentials from .env.development + localhost connections from launchSettings.json
// Issue #2152: Skip .env loading in CI environment to prevent env var corruption
var aspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
var isDevelopmentEnv = string.Equals(aspNetCoreEnv, "Development", StringComparison.OrdinalIgnoreCase);
var isCIEnv = string.Equals(aspNetCoreEnv, "CI", StringComparison.OrdinalIgnoreCase);

if (isDevelopmentEnv && !isCIEnv)
{
    var envFilePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".env.development");
    if (File.Exists(envFilePath))
    {
        Env.Load(envFilePath, new LoadOptions(setEnvVars: true, clobberExistingVars: false));
        Console.WriteLine("[DotNetEnv] Loaded .env.development for local development");
    }
    else
    {
        // Try alternative path (when running from solution root)
        var altEnvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env.development");
        if (File.Exists(altEnvPath))
        {
            Env.Load(envFilePath, new LoadOptions(setEnvVars: true, clobberExistingVars: false));
            Console.WriteLine("[DotNetEnv] Loaded .env.development for local development");
        }
    }
}
else if (isCIEnv)
{
    Console.WriteLine("[DotNetEnv] CI environment detected - skipping .env.development loading");
}

var builder = WebApplication.CreateBuilder(args);

// OPS-04: Configure Serilog with environment-based settings and sensitive data redaction
Log.Logger = LoggingConfiguration.ConfigureSerilog(builder).CreateLogger();

builder.Host.UseSerilog();

// ISSUE-2510: Validate secrets from infra/secrets/ directory
// Load and validate all secrets with 3-level validation (Critical/Important/Optional)
// Note: Using temporary logger factory since DI container not yet built
// Issue #2556: Skip secret validation in Testing/CI environments (used by integration/E2E tests)
if (!builder.Environment.IsEnvironment("Testing") && !builder.Environment.IsEnvironment("CI"))
{
    using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder.AddSerilog(Log.Logger));
    var tempLogger = loggerFactory.CreateLogger<Program>();
    var secretLoader = new Api.Infrastructure.Configuration.SecretLoader(builder.Configuration, tempLogger);
    var secretValidationResult = secretLoader.LoadAndValidate();

    if (!secretValidationResult.IsValid)
    {
        Log.Fatal(
            "Application startup failed: CRITICAL secrets missing. Please configure secret files in infra/secrets/. " +
            "Missing: {MissingSecrets}",
            string.Join(", ", secretValidationResult.MissingCritical)
        );
        throw new InvalidOperationException(
            $"Critical secrets missing: {string.Join(", ", secretValidationResult.MissingCritical)}. " +
            "See infra/secrets.example/ for templates."
        );
    }

    // Log warnings for missing important secrets (startup continues but with reduced functionality)
    if (secretValidationResult.HasWarnings)
    {
        Log.Warning(
            "Some secrets are missing or have errors. Application will start but some features may be disabled. " +
            "Check logs above for details or see infra/secrets.example/ for templates."
        );
    }
}
else
{
    Log.Information("Testing/CI environment detected - skipping secret validation");
}

// BGAI-081: Cookie Policy for Development
// Allow SameSite=None without Secure for localhost cross-port development
// In production, this should be removed (SameSite=None REQUIRES Secure=true)
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
    });
}

#pragma warning disable S125 // Sections of code should not be commented out
// PERF-11: Response Compression DISABLED - causing ERR_CONTENT_DECODING_FAILED in Docker
// NOTE: Re-enable with proper configuration after investigating compression issue
// builder.Services.AddResponseCompression(options =>
// {
//     options.EnableForHttps = true; // Enable compression for HTTPS (secure)
//     options.Providers.Add<GzipCompressionProvider>(); // Gzip (fallback, widely supported)
//
//     // Compress these MIME types
//     options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(AdditionalCompressedMimeTypes);
// });

// Configure Brotli compression level (optimal balance) - DISABLED
// builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
// {
//     options.Level = CompressionLevel.Fastest; // Fastest: lower CPU, good compression (1-5x faster than Optimal)
// });

// Configure Gzip compression level (optimal balance) - DISABLED
// builder.Services.Configure<GzipCompressionProviderOptions>(options =>
// {
//     options.Level = CompressionLevel.Fastest; // Fastest: lower CPU, good compression
// });
#pragma warning restore S125

var forwardedHeadersSection = builder.Configuration.GetSection("ForwardedHeaders");
var forwardedHeadersEnabled = forwardedHeadersSection.GetValue<bool?>("Enabled") ?? true;

if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        var knownProxies = forwardedHeadersSection.GetSection("KnownProxies").Get<string[]>() ?? Array.Empty<string>();
        foreach (var proxy in knownProxies)
        {
            if (!string.IsNullOrWhiteSpace(proxy) && IPAddress.TryParse(proxy, out var proxyAddress))
            {
                options.KnownProxies.Add(proxyAddress);
            }
        }

        var knownNetworks = forwardedHeadersSection.GetSection("KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
        foreach (var network in knownNetworks)
        {
            if (string.IsNullOrWhiteSpace(network))
            {
                continue;
            }

            if (AspNetIpNetwork.TryParse(network, out var ipNetwork))
            {
                options.KnownNetworks.Add(ipNetwork);
                continue;
            }

            var parts = network.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var networkAddress) && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var prefixLength))
            {
                options.KnownNetworks.Add(new AspNetIpNetwork(networkAddress, prefixLength));
            }
        }

        var forwardLimit = forwardedHeadersSection.GetValue<int?>("ForwardLimit");
        if (forwardLimit != null && forwardLimit.HasValue)
        {
            options.ForwardLimit = forwardLimit.Value;
        }

        var requireHeaderSymmetry = forwardedHeadersSection.GetValue<bool?>("RequireHeaderSymmetry");
        if (requireHeaderSymmetry != null && requireHeaderSymmetry.HasValue)
        {
            options.RequireHeaderSymmetry = requireHeaderSymmetry.Value;
        }
    });
}

builder.Services.Configure<SessionCookieConfiguration>(builder.Configuration.GetSection("Authentication:SessionCookie"));
builder.Services.Configure<SessionManagementConfiguration>(builder.Configuration.GetSection("Authentication:SessionManagement"));
builder.Services.Configure<OAuthConfiguration>(builder.Configuration.GetSection("Authentication:OAuth")); // AUTH-06
builder.Services.Configure<RateLimitConfiguration>(builder.Configuration.GetSection("RateLimit"));
// DDD-PHASE4: PdfProcessingConfiguration removed - PDF config now in DocumentProcessing bounded context
builder.Services.Configure<FollowUpQuestionsConfiguration>(builder.Configuration.GetSection("FollowUpQuestions")); // CHAT-02
builder.Services.Configure<RagPromptsConfiguration>(builder.Configuration.GetSection("RagPrompts")); // AI-07.1: RAG prompt templates
builder.Services.Configure<HybridCacheConfiguration>(builder.Configuration.GetSection("HybridCache")); // PERF-05: HybridCache configuration
builder.Services.Configure<HybridSearchConfiguration>(builder.Configuration.GetSection("HybridSearch")); // AI-14: Hybrid search configuration
builder.Services.Configure<WeeklyEvaluationConfiguration>(builder.Configuration.GetSection("QualityEvaluation")); // BGAI-042: Weekly evaluation configuration
builder.Services.Configure<Api.BoundedContexts.Administration.Infrastructure.External.PrometheusOptions>(builder.Configuration.GetSection("Prometheus")); // Issue #893: Prometheus HTTP client configuration
builder.Services.Configure<IndexingSettings>(builder.Configuration.GetSection(IndexingSettings.SectionName)); // ISSUE-3197: Vector indexing batch configuration

// Issue #1447: Security headers middleware configuration
builder.Services.AddSecurityHeaders(builder.Configuration);

// BGAI-021 (Issue #963): AI provider configuration with startup validation
builder.Services.Configure<AiProviderSettings>(builder.Configuration.GetSection(AiProviderSettings.SectionName));
builder.Services.AddSingleton<IValidateOptions<AiProviderSettings>, AiProviderValidator>();
builder.Services.AddOptions<AiProviderSettings>().ValidateOnStart(); // Trigger validation on startup

// Infrastructure services (DB, Cache, HTTP clients)
builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

// Register TimeProvider for dependency injection (TEST-814: Enables test override)
// In production, uses TimeProvider.System. Tests can override with TestTimeProvider/FakeTimeProvider.
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// SEC-07: Issue #1787 - TOTP Replay Attack Prevention Background Cleanup
builder.Services.AddHostedService<Api.Infrastructure.BackgroundTasks.UsedTotpCodeCleanupTask>();

// Issue #1449: FluentValidation for CQRS pipeline
builder.Services.AddFluentValidation();

// DDD-PHASE1: MediatR for CQRS (Commands, Queries, Handlers)
// Issue #1449: Add ValidationBehavior to MediatR pipeline
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(Api.SharedKernel.Application.Behaviors.ValidationBehavior<,>));
});

// Application services (Domain, AI, Admin)
// BGAI-001-v2: Pass configuration for PDF extractor provider selection
#pragma warning disable CS0618 // Type or member is obsolete
builder.Services.AddApplicationServices(builder.Configuration);
#pragma warning restore CS0618


// Authentication services (Auth, OAuth, 2FA, API keys, Sessions)
builder.Services.AddAuthenticationServices(builder.Configuration);


// Observability services (OpenTelemetry, Health checks, Swagger)
builder.Services.AddObservabilityServices(builder.Configuration, builder.Environment);

// ISSUE #2424: Rate limiting for API protection
// Issue #2705: Pass configuration to allow disabling in integration tests
builder.Services.AddRateLimitingServices(builder.Configuration);

// Configure JSON serialization for ASP.NET Core Minimal APIs
// Accept camelCase from frontend (JavaScript convention) while backend uses PascalCase (C# convention)
// NOTE: ASP.NET Core 9.0 Minimal APIs require unified JsonOptions configuration
// Reference: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding

// Configure BOTH request (parameter binding) AND response (Results.Ok) serialization
// This single configuration applies to all JSON operations in Minimal APIs
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Case-insensitive deserialization allows both {"email":...} and {"Email":...} to work
    // This is critical for frontend/backend interoperability (JS uses camelCase, C# uses PascalCase)
    options.SerializerOptions.PropertyNameCaseInsensitive = true;

    // Allow trailing commas in JSON for tolerant parsing (e.g., {"a":1,} is valid)
    options.SerializerOptions.AllowTrailingCommas = true;

    // Use camelCase for JSON serialization (JavaScript convention)
    // PropertyNameCaseInsensitive handles deserialization, this handles serialization
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

    // Serialize enums as strings (e.g., "UserRegistered" instead of 0)
    // Required for frontend schema validation (zod expects string enum values)
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var corsOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        var topLevelOrigins = builder.Configuration
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
                "traceparent",  // W3C Trace Context propagation
                "tracestate"    // W3C Trace Context state
            )
            .AllowAnyMethod()
            .AllowCredentials()
            // Issue #1563 (P0-3): Expose trace headers for frontend correlation
            .WithExposedHeaders("X-Trace-Id", "X-Span-Id", "traceparent", "tracestate");
    });
});

// Issue #2406: SignalR for real-time game state updates
builder.Services.AddSignalR();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetService<MeepleAiDbContext>();

    if (db == null)
    {
        // Logging tests and other scenarios without DB context
        app.Logger.LogInformation("Skipping database migrations (MeepleAiDbContext not registered)");
    }
    else if (ShouldSkipMigrations(app, db))
    {
        // Test or in-memory environments create the schema elsewhere.
    }
    else
    {
        await db.Database.MigrateAsync().ConfigureAwait(false);

        // AI-01: Initialize Qdrant collection
        var qdrant = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        await qdrant.EnsureCollectionExistsAsync().ConfigureAwait(false);

        // Validate embedding configuration consistency
        var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var embeddingDimensions = embedding.GetEmbeddingDimensions();
        var provider = app.Configuration["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";
        var model = app.Configuration["EMBEDDING_MODEL"] ?? "nomic-embed-text";

        app.Logger.LogInformation("✓ Embedding configuration validated: Provider={Provider}, Model={Model}, Dimensions={Dimensions}",
            provider, model, embeddingDimensions);

        // ISSUE-2512: Auto-configuration pipeline - Seed admin user, test user, AI models,
        // shared games, badges, and rate limits (environment-independent, runs on first startup only)
        var autoConfigService = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.Administration.Application.Services.IAutoConfigurationService>();
        await autoConfigService.InitializeAsync().ConfigureAwait(false);
    }
}

// Configure middleware pipeline
app.ConfigureMiddlewarePipeline(forwardedHeadersEnabled);

// Unversioned endpoints (keep for backwards compatibility and infrastructure)
app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

// ISSUE-2511: Comprehensive health check endpoint
app.MapHealthCheckEndpoints();

// OPS-01: Health check endpoints (backwards compatibility)
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                tags = e.Value.Tags
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result).ConfigureAwait(false);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("cache") || check.Tags.Contains("vector")
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Just check if the app is running
});

// CA1869: Cache JsonSerializerOptions for health check endpoints
var healthCheckJsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };

app.MapHealthChecks("/health/config", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("configuration"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        }, healthCheckJsonOptions);
        await context.Response.WriteAsync(result).ConfigureAwait(false);
    }
});

// API-01: Create v1 API route group and map routing files
var v1Api = app.MapGroup("/api/v1");

v1Api.MapAuthEndpoints();
v1Api.MapShareLinkEndpoints(); // ISSUE-2052: Shareable chat thread links
v1Api.MapUserProfileEndpoints();
v1Api.MapGameEndpoints();
v1Api.MapSessionTrackingEndpoints(); // GST-003: Session tracking real-time collaboration
v1Api.MapSharedGameCatalogEndpoints(); // ISSUE-2371: Shared game catalog Phase 2
v1Api.MapRulebookAnalysisEndpoints(); // ISSUE-2402: Rulebook analysis service
v1Api.MapLlmEndpoints(); // ISSUE-2391: Sprint 2 - LLM provider management
v1Api.MapAiEndpoints();
v1Api.MapPdfEndpoints();
v1Api.MapDocumentCollectionEndpoints();
v1Api.MapRuleSpecEndpoints();

// Issue #1439: Split AdminEndpoints into focused endpoint files
v1Api.MapConfigurationEndpoints();     // System configuration CRUD & operations
v1Api.MapRateLimitAdminEndpoints();    // Issue #2738: Rate limit admin management
// v1Api.MapAiModelEndpoints();        // Issue #2580: REMOVED - Replaced by MapAiModelAdminEndpoints (Issue #2567)
v1Api.MapGameLibraryConfigEndpoints(); // Issue #2444: Game library tier limits config
v1Api.MapSessionLimitsConfigEndpoints(); // Issue #3070: Session limits config
v1Api.MapPdfUploadLimitsConfigEndpoints(); // Issue #3072: PDF upload limits config
v1Api.MapAnalyticsEndpoints();         // Dashboard statistics & metrics
v1Api.MapDashboardEndpoints();         // Issue #3314: User dashboard aggregated API
v1Api.MapLlmAnalyticsEndpoints();      // ISSUE-1725: LLM cost optimization analytics
v1Api.MapMonitoringEndpoints();        // Issues #891 + #893: Infrastructure health & Prometheus metrics
v1Api.MapAlertEndpoints();             // Alert management
v1Api.MapAlertConfigEndpoints();       // Alert rules (Issue #921)
v1Api.MapAlertConfigurationEndpoints(); // Alert configuration (Issue #915)
v1Api.MapNotificationEndpoints();      // User notifications (Issue #2053)
v1Api.MapUserLibraryEndpoints();       // User game library
v1Api.MapAuditEndpoints();             // Audit log retrieval & search
v1Api.MapFeatureFlagEndpoints();       // Feature flag management
v1Api.MapPromptManagementEndpoints();  // Prompt templates & evaluation
v1Api.MapWorkflowEndpoints();          // n8n workflow integration
v1Api.MapSessionEndpoints();           // Session management
v1Api.MapApiKeyEndpoints();            // API key management
v1Api.MapCacheEndpoints();             // Cache management
v1Api.MapAdminUserEndpoints();         // User management
v1Api.MapAiModelAdminEndpoints();      // AI model management (Issue #2567)
v1Api.MapAdminMiscEndpoints();         // Miscellaneous admin operations
v1Api.MapReportingEndpoints();         // ISSUE-916: Report generation & scheduling
v1Api.MapTestingMetricsEndpoints();    // Issue #2139: Testing metrics API

// DDD-PHASE3: KnowledgeBase bounded context endpoints
v1Api.MapKnowledgeBaseEndpoints();
v1Api.MapLedgerModeEndpoints();     // Issue #2405: Ledger Mode endpoints

// Issue #866: Agent management endpoints
v1Api.MapAgentEndpoints();

// Issue #3377: AI model configuration endpoints
v1Api.MapModelEndpoints();

// Issue #3177, #3178: Agent typology endpoints (AGT-003, AGT-004)
v1Api.MapGroup("/agent-typologies").MapAgentTypologyEndpoints();

// Issue #3184 (AGT-010): Agent session lifecycle endpoints
v1Api.MapAgentSessionEndpoints();

// Issue #3483: Chat session persistence endpoints
v1Api.MapChatSessionEndpoints();

// Issue #1565: Telemetry test endpoints for HyperDX integration testing
v1Api.MapTelemetryTestEndpoints();
v1Api.MapTestTelemetryEndpoints(); // Issue #1567: Manual span test endpoint

// Issue #2004: Runbook validation test endpoints
v1Api.MapTestEndpoints();

// Issue #2406: SignalR hub for real-time game state updates
app.MapHub<Api.Hubs.GameStateHub>("/hubs/gamestate");

// ISSUE-2511: Startup health check for critical services
using (var scope = app.Services.CreateScope())
{
    var healthCheckService = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
    var healthLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var startupCheck = await healthCheckService.CheckHealthAsync(
        check => check.Tags.Contains(Api.Infrastructure.Health.Models.HealthCheckTags.Critical)).ConfigureAwait(false);

    var criticalFailures = startupCheck.Entries
        .Where(e => e.Value.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
        .ToList();

    if (criticalFailures.Count > 0)
    {
        var failedServices = string.Join(", ", criticalFailures.Select(f => f.Key));
        healthLogger.LogCritical(
            "Critical services failed startup health check: {Services}. Application starting in degraded mode.",
            failedServices);
    }
    else
    {
        healthLogger.LogInformation("All critical services passed startup health check.");
    }
}

await app.RunAsync().ConfigureAwait(false);

// ISSUE-2512: Removed 300+ lines of obsolete bootstrap functions:
// - EnsureInitialAdminUserAsync → Replaced by AutoConfigurationService + SeedAdminUserCommand
// - EnsureTestUserExistsAsync → Replaced by AutoConfigurationService + SeedTestUserCommand
// - Helper functions (TryGetAdminCredentials, CreateAdminUser, IsUniqueConstraintViolation, etc.) → Now in CQRS handlers
// New pattern: CQRS commands (testable, maintainable, idempotent) instead of Program.cs helpers

// OPS-01: Helper method for database migration logic
static bool ShouldSkipMigrations(WebApplication app, MeepleAiDbContext db)
{
    // Skip migrations in Testing and CI environments (E2E tests use Testcontainers)
    if (app.Environment.IsEnvironment("Testing") || app.Environment.IsEnvironment("CI"))
    {
        return true;
    }

    if (app.Configuration.GetValue<bool?>("SkipMigrations").GetValueOrDefault(false))
    {
        return true;
    }

    var providerName = db.Database.ProviderName;
    if (providerName != null && providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var connection = db.Database.GetDbConnection();
        var connectionString = connection?.ConnectionString;
        var dataSource = connection?.DataSource;

        if (!string.IsNullOrWhiteSpace(connectionString) &&
            (connectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase) ||
             connectionString.Contains("mode=memory", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(dataSource) && dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

#pragma warning disable S1118 // Utility classes should not have public constructors - Required for test integration
public partial class Program
{
    internal static readonly string[] AdditionalCompressedMimeTypes =
    [
        "application/json",
        "application/json; charset=utf-8",
        "text/plain",
        "text/json",
        "application/xml",
        "text/xml",
        "image/svg+xml"
    ];
}
