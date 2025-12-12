using Api.Configuration; // CHAT-02
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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

var builder = WebApplication.CreateBuilder(args);

// OPS-04: Configure Serilog with environment-based settings and sensitive data redaction
Log.Logger = LoggingConfiguration.ConfigureSerilog(builder).CreateLogger();

builder.Host.UseSerilog();

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

// PERF-11: Configure Response Compression (Brotli + Gzip)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Enable compression for HTTPS (secure)
    options.Providers.Add<BrotliCompressionProvider>(); // Brotli (better compression)
    options.Providers.Add<GzipCompressionProvider>(); // Gzip (fallback, widely supported)

    // Compress these MIME types
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/json; charset=utf-8",
        "text/plain",
        "text/json",
        "application/xml",
        "text/xml",
        "image/svg+xml"
    });
});

// Configure Brotli compression level (optimal balance)
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Fastest: lower CPU, good compression (1-5x faster than Optimal)
});

// Configure Gzip compression level (optimal balance)
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Fastest: lower CPU, good compression
});

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
builder.Services.AddApplicationServices(builder.Configuration);

// Authentication services (Auth, OAuth, 2FA, API keys, Sessions)
builder.Services.AddAuthenticationServices(builder.Configuration);


// Observability services (OpenTelemetry, Health checks, Swagger)
builder.Services.AddObservabilityServices(builder.Configuration, builder.Environment);

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
        policy
            .WithHeaders(
                "Content-Type",
                "Authorization",
                "X-Correlation-ID"
            )
            .AllowAnyMethod()
            .AllowCredentials()
            // Issue #1563 (P0-3): Expose trace headers for frontend correlation
            .WithExposedHeaders("X-Trace-Id", "X-Span-Id");
    });
});

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
        db.Database.Migrate();

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

        // Bootstrap: Create initial admin user if database is empty
        await EnsureInitialAdminUserAsync(app, db, scope.ServiceProvider).ConfigureAwait(false);

        // K6 Performance Testing: Ensure test user exists in Development/Test environments (Issue #1663)
        if (app.Environment.IsDevelopment() || string.Equals(app.Environment.EnvironmentName, "Test", StringComparison.Ordinal))
        {
            await EnsureTestUserExistsAsync(app, db, scope.ServiceProvider).ConfigureAwait(false);
        }
    }
}

// Configure middleware pipeline
app.ConfigureMiddlewarePipeline(forwardedHeadersEnabled);

// Unversioned endpoints (keep for backwards compatibility and infrastructure)
app.MapGet("/", () => Results.Json(new { ok = true, name = "MeepleAgentAI" }));

// OPS-01: Health check endpoints
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

// API-01: Create v1 API route group and map routing files
var v1Api = app.MapGroup("/api/v1");

v1Api.MapAuthEndpoints();
v1Api.MapUserProfileEndpoints();
v1Api.MapGameEndpoints();
v1Api.MapAiEndpoints();
v1Api.MapPdfEndpoints();
v1Api.MapRuleSpecEndpoints();

// Issue #1439: Split AdminEndpoints into focused endpoint files
v1Api.MapConfigurationEndpoints();     // System configuration CRUD & operations
v1Api.MapAnalyticsEndpoints();         // Dashboard statistics & metrics
v1Api.MapLlmAnalyticsEndpoints();      // ISSUE-1725: LLM cost optimization analytics
v1Api.MapMonitoringEndpoints();        // Issues #891 + #893: Infrastructure health & Prometheus metrics
v1Api.MapAlertEndpoints();             // Alert management
v1Api.MapAlertConfigEndpoints();       // Alert rules (Issue #921)
v1Api.MapAlertConfigurationEndpoints(); // Alert configuration (Issue #915)
v1Api.MapAuditEndpoints();             // Audit log retrieval & search
v1Api.MapFeatureFlagEndpoints();       // Feature flag management
v1Api.MapPromptManagementEndpoints();  // Prompt templates & evaluation
v1Api.MapWorkflowEndpoints();          // n8n workflow integration
v1Api.MapSessionEndpoints();           // Session management
v1Api.MapApiKeyEndpoints();            // API key management
v1Api.MapCacheEndpoints();             // Cache management
v1Api.MapAdminUserEndpoints();         // User management
v1Api.MapAdminMiscEndpoints();         // Miscellaneous admin operations
v1Api.MapReportingEndpoints();         // ISSUE-916: Report generation & scheduling

// DDD-PHASE3: KnowledgeBase bounded context endpoints
v1Api.MapKnowledgeBaseEndpoints();

// Issue #866: Agent management endpoints
v1Api.MapAgentEndpoints();

// Issue #1565: Telemetry test endpoints for HyperDX integration testing
v1Api.MapTelemetryTestEndpoints();
v1Api.MapTestTelemetryEndpoints(); // Issue #1567: Manual span test endpoint

// Issue #2004: Runbook validation test endpoints
v1Api.MapTestEndpoints();

app.Run();

#pragma warning disable MA0051 // Method is too long - Bootstrap method requires comprehensive validation and error handling
// Bootstrap: Create initial admin user if database is empty
static async Task EnsureInitialAdminUserAsync(WebApplication app, MeepleAiDbContext db, IServiceProvider services)
{
    // Skip in test environments
    if (app.Environment.IsEnvironment("Testing"))
    {
        return;
    }

    // Check if any admin users exist
    var hasAdminUser = await db.Users
        .AnyAsync(u => u.Role == "admin").ConfigureAwait(false);

    if (hasAdminUser)
    {
        app.Logger.LogInformation("Admin user already exists, skipping bootstrap");
        return;
    }

    // Get admin credentials from environment variables
    var adminEmail = app.Configuration["INITIAL_ADMIN_EMAIL"];
    // SEC-708: Read initial admin password from Docker Secret file or direct config
    var adminPassword = SecretsHelper.GetSecretOrValue(
        app.Configuration,
        "INITIAL_ADMIN_PASSWORD",
        app.Logger,
        required: false
    );
    var adminDisplayName = app.Configuration["INITIAL_ADMIN_DISPLAY_NAME"] ?? "System Admin";

    if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    {
        app.Logger.LogWarning(
            "⚠️  No admin user found and INITIAL_ADMIN_EMAIL/INITIAL_ADMIN_PASSWORD not set. " +
            "Please create an admin user manually or set environment variables."
        );
        return;
    }

    // Validate email format
    if (!adminEmail.Contains('@'))
    {
        app.Logger.LogError("Invalid INITIAL_ADMIN_EMAIL format: {Email}", adminEmail);
        return;
    }

    // Validate password strength (minimum 8 chars, at least one uppercase, one digit)
    if (adminPassword.Length < 8 ||
        !adminPassword.Any(char.IsUpper) ||
        !adminPassword.Any(char.IsDigit))
    {
        app.Logger.LogError(
            "INITIAL_ADMIN_PASSWORD must be at least 8 characters with uppercase and digit"
        );
        return;
    }

    try
    {
        // Hash the password using PBKDF2
        var passwordHashingService = services.GetRequiredService<IPasswordHashingService>();
        var passwordHash = passwordHashingService.HashSecret(adminPassword);

        // Create admin user
        var adminUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            DisplayName = adminDisplayName,
            PasswordHash = passwordHash,
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(adminUser);
        await db.SaveChangesAsync().ConfigureAwait(false);

        app.Logger.LogInformation(
            "✅ Initial admin user created successfully: {Email} (ID: {UserId})",
            adminEmail,
            adminUser.Id
        );

        // Audit log
        var auditLog = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = null, // System-generated
            Action = "BOOTSTRAP_ADMIN_CREATED",
            Resource = "User",
            ResourceId = adminUser.Id.ToString(),
            Result = "Success",
            Details = $"Initial admin user created: {adminEmail}",
            IpAddress = "system",
            UserAgent = "bootstrap",
            CreatedAt = DateTime.UtcNow
        };

        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
    // RACE CONDITION PATTERN: Log unexpected unique constraint violations before propagating.
    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
#pragma warning restore S2139
    {
        // Race condition: Another instance created the admin user simultaneously
        // Recheck if an admin user now exists
        var adminNowExists = await db.Users
            .AnyAsync(u => u.Role == "admin").ConfigureAwait(false);

        if (adminNowExists)
        {
            app.Logger.LogInformation(
                "ℹ️  Admin user creation skipped: Another instance created the admin user concurrently"
            );
            return;
        }

        // If no admin exists, this is an unexpected error - rethrow
        app.Logger.LogError(ex, "Unique constraint violation but no admin user found");
        throw;
    }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
    // BOOTSTRAP PATTERN: Log critical initialization failures before propagating.
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to create initial admin user");
        throw;
    }
#pragma warning restore S2139
}
#pragma warning restore MA0051

#pragma warning disable MA0051 // Method is too long - Bootstrap method handles multiple test users with validation
// K6 Performance Testing: Ensure demo test users exist for testing (Issue #1663)
static async Task EnsureTestUserExistsAsync(WebApplication app, MeepleAiDbContext db, IServiceProvider services)
{
    var passwordHashingService = services.GetRequiredService<IPasswordHashingService>();
    const string demoPassword = "Demo123!";
    var passwordHash = passwordHashingService.HashSecret(demoPassword);

    // Demo users expected by Postman/Newman and K6 tests
    var demoUsers = new[]
    {
        new { Email = "user@meepleai.dev", DisplayName = "Demo User", Role = "User" },
        new { Email = "editor@meepleai.dev", DisplayName = "Demo Editor", Role = "Editor" },
        // Note: admin@meepleai.dev is created by EnsureInitialAdminUserAsync
    };

    foreach (var userData in demoUsers)
    {
        // Check if user already exists
        var userExists = await db.Users.AnyAsync(u => u.Email == userData.Email).ConfigureAwait(false);

        if (userExists)
        {
            app.Logger.LogDebug("ℹ️  Demo user already exists: {Email}", userData.Email);
            continue;
        }

        try
        {
            var demoUser = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = userData.Email,
                DisplayName = userData.DisplayName,
                PasswordHash = passwordHash,
                Role = userData.Role,
                CreatedAt = DateTime.UtcNow,
                IsDemoAccount = true
            };

            db.Users.Add(demoUser);
            await db.SaveChangesAsync().ConfigureAwait(false);

            app.Logger.LogInformation(
                "✅ Demo user created: {Email} ({Role}) - for Postman/Newman and K6 testing",
                userData.Email,
                userData.Role
            );

            // Audit log
            var auditLog = new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = null,
                Action = "DEMO_USER_CREATED",
                Resource = "User",
                ResourceId = demoUser.Id.ToString(),
                Result = "Success",
                Details = $"Demo user created: {userData.Email} ({userData.Role})",
                IpAddress = "system",
                UserAgent = "bootstrap",
                CreatedAt = DateTime.UtcNow
            };

            db.AuditLogs.Add(auditLog);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            app.Logger.LogDebug("ℹ️  Demo user creation skipped: {Email} already exists concurrently", userData.Email);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to create demo user {Email} - non-critical for production", userData.Email);
        }
    }

    app.Logger.LogInformation("✓ Demo user seeding complete");
}
#pragma warning restore MA0051

// Helper method to detect unique constraint violations across database providers
static bool IsUniqueConstraintViolation(DbUpdateException ex)
{
    var innerException = ex.InnerException;
    if (innerException == null)
    {
        return false;
    }

    var message = innerException.Message.ToLowerInvariant();

    // PostgreSQL: "23505: duplicate key value violates unique constraint"
    if (message.Contains("23505") || message.Contains("duplicate key"))
    {
        return true;
    }

    // SQLite: "UNIQUE constraint failed"
    if (message.Contains("unique constraint"))
    {
        return true;
    }

    // SQL Server: "Cannot insert duplicate key"
    if (message.Contains("cannot insert duplicate key") || message.Contains("violation of unique key"))
    {
        return true;
    }

    return false;
}

// OPS-01: Helper method for database migration logic
static bool ShouldSkipMigrations(WebApplication app, MeepleAiDbContext db)
{
    if (app.Environment.IsEnvironment("Testing"))
    {
        return true;
    }

    if (app.Configuration.GetValue<bool?>("SkipMigrations") == true)
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
public partial class Program { }
