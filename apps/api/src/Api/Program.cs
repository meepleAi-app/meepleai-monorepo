using Api.Infrastructure;
using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Api.Infrastructure.Entities;
using Api.Middleware;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using AspNetIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
// OPS-02: OpenTelemetry imports
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Api.Observability;
// OPS-04: Structured logging imports
using Api.Logging;

var builder = WebApplication.CreateBuilder(args);

// OPS-04: Configure Serilog with environment-based settings and sensitive data redaction
Log.Logger = LoggingConfiguration.ConfigureSerilog(builder).CreateLogger();

builder.Host.UseSerilog();

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
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var networkAddress) && int.TryParse(parts[1], out var prefixLength))
            {
                options.KnownNetworks.Add(new AspNetIpNetwork(networkAddress, prefixLength));
            }
        }

        var forwardLimit = forwardedHeadersSection.GetValue<int?>("ForwardLimit");
        if (forwardLimit.HasValue)
        {
            options.ForwardLimit = forwardLimit.Value;
        }

        var requireHeaderSymmetry = forwardedHeadersSection.GetValue<bool?>("RequireHeaderSymmetry");
        if (requireHeaderSymmetry.HasValue)
        {
            options.RequireHeaderSymmetry = requireHeaderSymmetry.Value;
        }
    });
}

builder.Services.Configure<SessionCookieConfiguration>(builder.Configuration.GetSection("Authentication:SessionCookie"));
builder.Services.Configure<SessionManagementConfiguration>(builder.Configuration.GetSection("Authentication:SessionManagement"));
builder.Services.Configure<RateLimitConfiguration>(builder.Configuration.GetSection("RateLimit"));

// Only configure Postgres in non-test environments (tests will override with SQLite)
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? builder.Configuration["ConnectionStrings__Postgres"]
        ?? throw new InvalidOperationException("Missing Postgres connection string");

    builder.Services.AddDbContext<MeepleAiDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Configure Redis
var redisUrl = builder.Configuration["REDIS_URL"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisUrl);
    configuration.AbortOnConnectFail = false; // Fail gracefully if Redis unavailable
    return ConnectionMultiplexer.Connect(configuration);
});

// Configure HttpClient for EmbeddingService and LlmService
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Ollama", client =>
{
    var ollamaUrl = builder.Configuration["OLLAMA_URL"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("OpenRouter", client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Time provider for testability
builder.Services.AddSingleton(TimeProvider.System);

// Background task execution
builder.Services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

// AI-01: Vector search services
builder.Services.AddSingleton<IQdrantClientAdapter, QdrantClientAdapter>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILlmService, OllamaLlmService>();
builder.Services.AddScoped<ITextChunkingService, TextChunkingService>();
builder.Services.AddScoped<PdfIndexingService>();

// AI-05: AI response caching
builder.Services.AddSingleton<IAiResponseCacheService, AiResponseCacheService>();

// PERF-02: Session caching (Phase 2 optimization)
builder.Services.AddSingleton<ISessionCacheService, SessionCacheService>();

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<RuleSpecService>();
builder.Services.AddScoped<RuleSpecDiffService>();
builder.Services.AddScoped<RuleSpecCommentService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<IStreamingRagService, StreamingRagService>(); // API-02: Streaming RAG service
builder.Services.AddScoped<IStreamingQaService, StreamingQaService>(); // CHAT-01: Streaming QA service
builder.Services.AddScoped<SetupGuideService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AiRequestLogService>();
builder.Services.AddScoped<AgentFeedbackService>();
builder.Services.AddScoped<RateLimitService>();
builder.Services.AddScoped<PdfTextExtractionService>();
builder.Services.AddScoped<PdfTableExtractionService>();
builder.Services.AddScoped<PdfStorageService>();
builder.Services.AddScoped<N8nConfigService>();
builder.Services.AddScoped<ChatService>();

// AUTH-03: Session management service
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddHostedService<SessionAutoRevocationService>();

// AI-06: RAG offline evaluation service
builder.Services.AddScoped<IRagEvaluationService, RagEvaluationService>();

// API-01: API key authentication service
builder.Services.AddScoped<ApiKeyAuthenticationService>();

// API-04: API key management service
builder.Services.AddScoped<ApiKeyManagementService>();

// PDF-02: OCR service for fallback text extraction
builder.Services.AddSingleton<IOcrService, TesseractOcrService>();

// CHESS-03: Chess knowledge indexing service
builder.Services.AddScoped<IChessKnowledgeService, ChessKnowledgeService>();

// CHESS-04: Chess conversational agent service
builder.Services.AddScoped<IChessAgentService, ChessAgentService>();

// AI-07: Prompt versioning and management service
builder.Services.AddScoped<IPromptManagementService, PromptManagementService>();

// API-01: OpenAPI/Swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
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

// OPS-01: Health checks for observability
var healthCheckConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["ConnectionStrings__Postgres"];
var healthCheckRedisConnectionString = builder.Configuration["REDIS_URL"] ?? "localhost:6379";
var healthCheckQdrantUrl = builder.Configuration["QDRANT_URL"] ?? "http://localhost:6333";

builder.Services.AddHealthChecks()
    .AddNpgSql(
        healthCheckConnectionString ?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres",
        name: "postgres",
        tags: new[] { "db", "sql" })
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

        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

// OPS-02: OpenTelemetry configuration
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://jaeger:4318";
var serviceName = "MeepleAI.Api";
var serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing
        .SetSampler(new OpenTelemetry.Trace.AlwaysOnSampler())
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    if (ShouldSkipMigrations(app, db))
    {
        // Test or in-memory environments create the schema elsewhere.
    }
    else
    {
        db.Database.Migrate();

        // AI-01: Initialize Qdrant collection
        var qdrant = scope.ServiceProvider.GetRequiredService<IQdrantService>();
        await qdrant.EnsureCollectionExistsAsync();
    }
}

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

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
    context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
    await next();
});

// API-01: API key authentication middleware (must be before cookie auth)
app.UseApiKeyAuthentication();

// Authentication middleware
app.Use(async (context, next) =>
{
    var sessionCookieName = GetSessionCookieName(context);

    if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
        !string.IsNullOrWhiteSpace(token))
    {
        var auth = context.RequestServices.GetRequiredService<AuthService>();
        var session = await auth.ValidateSessionAsync(token, context.RequestAborted);
        if (session != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, session.User.Id),
                new(ClaimTypes.Email, session.User.Email),
                new("displayName", session.User.DisplayName ?? string.Empty),
                new(ClaimTypes.Role, session.User.Role)
            };
            var identity = new ClaimsIdentity(claims, "session");
            context.User = new ClaimsPrincipal(identity);
            context.Items[nameof(ActiveSession)] = session;
        }
    }

    await next();
});

// Rate limiting middleware
app.Use(async (context, next) =>
{
    var rateLimiter = context.RequestServices.GetRequiredService<RateLimitService>();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Get rate limit key (prioritize authenticated user, fallback to IP)
    string rateLimitKey;
    RateLimitConfig config;

    if (context.Items.TryGetValue(nameof(ActiveSession), out var sessionObj) && sessionObj is ActiveSession session)
    {
        // Authenticated: rate limit per user + role
        rateLimitKey = $"user:{session.User.Id}";
        config = rateLimiter.GetConfigForRole(session.User.Role);
    }
    else
    {
        // Anonymous: rate limit per IP
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        rateLimitKey = $"ip:{ip}";
        config = rateLimiter.GetConfigForRole(null); // Default anonymous limits
    }

    var result = await rateLimiter.CheckRateLimitAsync(
        rateLimitKey,
        config.MaxTokens,
        config.RefillRate,
        context.RequestAborted);

    // Add rate limit headers
    context.Response.Headers["X-RateLimit-Limit"] = config.MaxTokens.ToString();
    context.Response.Headers["X-RateLimit-Remaining"] = result.TokensRemaining.ToString();

    if (!result.Allowed)
    {
        context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
        context.Response.StatusCode = 429; // Too Many Requests

        logger.LogWarning("Rate limit exceeded for {Key}. Retry after {RetryAfter}s",
            rateLimitKey, result.RetryAfterSeconds);

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            retryAfter = result.RetryAfterSeconds,
            message = $"Too many requests. Please try again in {result.RetryAfterSeconds} seconds."
        });
        return;
    }

    await next();
});

// API-04: API key quota enforcement middleware (must be after API key authentication)
app.UseMiddleware<ApiKeyQuotaEnforcementMiddleware>();

// API-01: Create v1 API route group
var v1Api = app.MapGroup("/api/v1");

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
        await context.Response.WriteAsync(result);
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

// API-01: Admin logs endpoint (versioned)
v1Api.MapGet("/logs", async (HttpContext context, AiRequestLogService logService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await logService.GetRequestsAsync(limit: 100, ct: ct);

    var response = result.Requests
        .Select(log =>
        {
            var level = string.Equals(log.Status, "Error", StringComparison.OrdinalIgnoreCase)
                ? "ERROR"
                : "INFO";

            var message = !string.IsNullOrWhiteSpace(log.ResponseSnippet)
                ? log.ResponseSnippet!
                : !string.IsNullOrWhiteSpace(log.Query)
                    ? log.Query!
                    : $"{log.Endpoint} request ({log.Status})";

            return new LogEntryResponse(
                log.CreatedAt,
                level,
                message,
                log.Id,
                log.UserId,
                log.GameId
            );
        })
        .ToList();

    return Results.Json(response);
});

// API-01: Authentication endpoints (versioned)
v1Api.MapPost("/auth/register", async (RegisterPayload payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        var command = new RegisterCommand(
            payload.Email,
            payload.Password,
            payload.DisplayName,
            payload.Role,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("User registration attempt for {Email}", payload.Email);
        var result = await auth.RegisterAsync(command, ct);
        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.Id, result.User.Role);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Registration validation failed for {Email}: {Error}", payload.Email, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Registration conflict for {Email}: {Error}", payload.Email, ex.Message);
        return Results.Conflict(new { error = ex.Message });
    }
});

v1Api.MapPost("/auth/login", async (LoginPayload? payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
{
    if (payload == null)
    {
        logger.LogWarning("Login failed: payload is null");
        return Results.BadRequest(new { error = "Invalid request payload" });
    }

    // Validate email and password are not empty
    if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
    {
        logger.LogWarning("Login failed: email or password is empty");
        return Results.BadRequest(new { error = "Email and password are required" });
    }

    try
    {
        var command = new LoginCommand(
            payload.Email,
            payload.Password,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());

        logger.LogInformation("Login attempt for {Email}", payload.Email);
        var result = await auth.LoginAsync(command, ct);
        if (result == null)
        {
            logger.LogWarning("Login failed for {Email}", payload.Email);
            RemoveSessionCookie(context);
            return Results.Unauthorized();
        }

        WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
        logger.LogInformation("User {UserId} logged in successfully", result.User.Id);
        return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login endpoint error");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

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
             connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(dataSource) &&
            dataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

v1Api.MapPost("/auth/logout", async (HttpContext context, AuthService auth, CancellationToken ct) =>
{
    var sessionCookieName = GetSessionCookieName(context);

    if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
        !string.IsNullOrWhiteSpace(token))
    {
        await auth.LogoutAsync(token, ct);
    }

    RemoveSessionCookie(context);
    return Results.Json(new { ok = true });
});

v1Api.MapGet("/auth/me", (HttpContext context) =>
{
    // Support both cookie-based session auth and API key auth
    if (context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession session)
    {
        return Results.Json(new AuthResponse(session.User, session.ExpiresAt));
    }

    // Check for API key authentication
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var displayName = context.User.FindFirst("displayName")?.Value;
        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(email))
        {
            var user = new AuthUser(userId, email, displayName ?? email, role ?? UserRole.User.ToString());
            return Results.Json(new AuthResponse(user, null)); // API keys don't have session expiration
        }
    }

    return Results.Unauthorized();
});

// AUTH-05: Session status and extension endpoints
v1Api.MapGet("/auth/session/status", async (
    HttpContext context,
    MeepleAiDbContext db,
    IConfiguration config,
    TimeProvider timeProvider,
    CancellationToken ct) =>
{
    // Require authentication
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var inactivityTimeoutDays = config.GetValue<int>("Authentication:SessionManagement:InactivityTimeoutDays", 30);

    // Get session from database to access LastSeenAt
    var sessionCookieName = GetSessionCookieName(context);
    if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
    var tokenHash = Convert.ToBase64String(hash);

    var dbSession = await db.UserSessions
        .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);

    if (dbSession == null || dbSession.RevokedAt != null)
    {
        return Results.Unauthorized();
    }

    // Calculate remaining minutes until session expires from inactivity
    var lastActivity = dbSession.LastSeenAt ?? dbSession.CreatedAt;
    var expiryTime = lastActivity.AddDays(inactivityTimeoutDays);
    var remainingMinutes = (int)Math.Max(0, (expiryTime - now).TotalMinutes);

    var response = new SessionStatusResponse(
        dbSession.ExpiresAt,
        dbSession.LastSeenAt,
        remainingMinutes);

    return Results.Json(response);
});

v1Api.MapPost("/auth/session/extend", async (
    HttpContext context,
    MeepleAiDbContext db,
    ISessionCacheService? sessionCache,
    IConfiguration config,
    TimeProvider timeProvider,
    CancellationToken ct) =>
{
    // Require authentication
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var now = timeProvider.GetUtcNow().UtcDateTime;
    var inactivityTimeoutDays = config.GetValue<int>("Authentication:SessionManagement:InactivityTimeoutDays", 30);

    // Get session from database
    var sessionCookieName = GetSessionCookieName(context);
    if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
    var tokenHash = Convert.ToBase64String(hash);

    var dbSession = await db.UserSessions
        .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);

    if (dbSession == null || dbSession.RevokedAt != null)
    {
        return Results.Unauthorized();
    }

    // Update LastSeenAt to extend session
    dbSession.LastSeenAt = now;
    await db.SaveChangesAsync(ct);

    // Invalidate cache to force refresh on next request
    if (sessionCache != null)
    {
        await sessionCache.InvalidateAsync(tokenHash, ct);
    }

    // Calculate new remaining minutes
    var expiryTime = now.AddDays(inactivityTimeoutDays);
    var remainingMinutes = (int)Math.Max(0, (expiryTime - now).TotalMinutes);

    var response = new SessionStatusResponse(
        dbSession.ExpiresAt,
        now,
        remainingMinutes);

    return Results.Json(response);
});

// API-01: AI agent endpoints (versioned)
v1Api.MapPost("/agents/qa", async (QaRequest req, HttpContext context, RagService rag, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("QA request from user {UserId} for game {GameId}: {Query}",
        session.User.Id, req.gameId, req.query);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                req.query,
                new { endpoint = "qa", gameId = req.gameId },
                ct);
        }

        var resp = await rag.AskAsync(req.gameId, req.query, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("QA response delivered for game {GameId}", req.gameId);

        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            if (resp.metadata.TryGetValue("model", out var metadataModel))
            {
                model = metadataModel;
            }

            if (resp.metadata.TryGetValue("finish_reason", out var metadataFinish))
            {
                finishReason = metadataFinish;
            }
        }

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.answer,
                new
                {
                    endpoint = "qa",
                    gameId = req.gameId,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    model,
                    finishReason,
                    snippetCount = resp.snippets.Count
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa",
            req.query,
            resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: model,
            finishReason: finishReason,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process QA request: {ex.Message}",
                    new { endpoint = "qa", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa",
            req.query,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

v1Api.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, RagService rag, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Explain request from user {UserId} for game {GameId}: {Topic}",
        session.User.Id, req.gameId, req.topic);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                $"Explain: {req.topic}",
                new { endpoint = "explain", gameId = req.gameId, topic = req.topic },
                ct);
        }

        var resp = await rag.ExplainAsync(req.gameId, req.topic, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Explain response delivered for game {GameId}, estimated {Minutes} min read",
            req.gameId, resp.estimatedReadingTimeMinutes);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.script,
                new
                {
                    endpoint = "explain",
                    gameId = req.gameId,
                    topic = req.topic,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    estimatedReadingTimeMinutes = resp.estimatedReadingTimeMinutes,
                    outline = resp.outline,
                    citationCount = resp.citations.Count
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "explain",
            req.topic,
            resp.script?.Length > 500 ? resp.script.Substring(0, 500) : resp.script,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: null,
            finishReason: null,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process Explain request: {ex.Message}",
                    new { endpoint = "explain", gameId = req.gameId, topic = req.topic, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "explain",
            req.topic,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

// API-02: Streaming RAG Explain endpoint (SSE)
v1Api.MapPost("/agents/explain/stream", async (ExplainRequest req, HttpContext context, IStreamingRagService streamingRag, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    logger.LogInformation("Streaming explain request from user {UserId} for game {GameId}: {Topic}",
        session.User.Id, req.gameId, req.topic);

    // Set SSE headers
    context.Response.Headers["Content-Type"] = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    try
    {
        await foreach (var evt in streamingRag.ExplainStreamAsync(req.gameId, req.topic, ct))
        {
            // Serialize event as JSON
            var json = System.Text.Json.JsonSerializer.Serialize(evt);

            // Write SSE format: "data: {json}\n\n"
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }

        logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}", req.gameId, req.topic);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Streaming explain cancelled by client for game {GameId}, topic: {Topic}", req.gameId, req.topic);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during streaming explain for game {GameId}, topic: {Topic}", req.gameId, req.topic);

        // Send error event if possible
        try
        {
            var errorEvent = new RagStreamingEvent(
                StreamingEventType.Error,
                new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                DateTime.UtcNow);
            var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
        catch
        {
            // If we can't send error event, client connection is likely broken
        }
    }

    return Results.Empty;
});

// CHAT-01: Streaming QA endpoint (SSE)
v1Api.MapPost("/agents/qa/stream", async (QaRequest req, HttpContext context, IStreamingQaService streamingQa, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Streaming QA request from user {UserId} for game {GameId}: {Query}",
        session.User.Id, req.gameId, req.query);

    // Set SSE headers
    context.Response.Headers["Content-Type"] = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                req.query,
                new { endpoint = "qa-stream", gameId = req.gameId },
                ct);
        }

        var answerBuilder = new System.Text.StringBuilder();
        var totalTokens = 0;
        double? confidence = null;
        var snippets = new List<Snippet>();

        await foreach (var evt in streamingQa.AskStreamAsync(req.gameId, req.query, req.chatId, ct))
        {
            // Serialize event as JSON
            var json = System.Text.Json.JsonSerializer.Serialize(evt);

            // Write SSE format: "data: {json}\n\n"
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            // Track response data for logging and chat persistence
            if (evt.Type == StreamingEventType.Token && evt.Data is System.Text.Json.JsonElement tokenElement)
            {
                var tokenData = System.Text.Json.JsonSerializer.Deserialize<StreamingToken>(tokenElement.GetRawText());
                if (tokenData != null)
                {
                    answerBuilder.Append(tokenData.token);
                }
            }
            else if (evt.Type == StreamingEventType.Citations && evt.Data is System.Text.Json.JsonElement citationsElement)
            {
                var citationsData = System.Text.Json.JsonSerializer.Deserialize<StreamingCitations>(citationsElement.GetRawText());
                if (citationsData != null)
                {
                    snippets = citationsData.citations.ToList();
                }
            }
            else if (evt.Type == StreamingEventType.Complete && evt.Data is System.Text.Json.JsonElement completeElement)
            {
                var completeData = System.Text.Json.JsonSerializer.Deserialize<StreamingComplete>(completeElement.GetRawText());
                if (completeData != null)
                {
                    totalTokens = completeData.totalTokens;
                    confidence = completeData.confidence;
                }
            }
        }

        var answer = answerBuilder.ToString();
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Streaming QA completed for game {GameId}, query: {Query}", req.gameId, req.query);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue && !string.IsNullOrWhiteSpace(answer))
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                answer,
                new
                {
                    endpoint = "qa-stream",
                    gameId = req.gameId,
                    totalTokens,
                    confidence,
                    snippetCount = snippets.Count
                },
                ct);
        }

        // Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa-stream",
            req.query,
            answer?.Length > 500 ? answer.Substring(0, 500) : answer,
            latencyMs,
            totalTokens,
            confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            completionTokens: totalTokens,
            ct: ct);
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Streaming QA cancelled by client for game {GameId}, query: {Query}", req.gameId, req.query);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        logger.LogError(ex, "Error during streaming QA for game {GameId}, query: {Query}", req.gameId, req.query);

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process streaming QA request: {ex.Message}",
                    new { endpoint = "qa-stream", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "qa-stream",
            req.query,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        // Send error event if possible
        try
        {
            var errorEvent = new RagStreamingEvent(
                StreamingEventType.Error,
                new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                DateTime.UtcNow);
            var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
        catch
        {
            // If we can't send error event, client connection is likely broken
        }
    }

    return Results.Empty;
});

// AI-03: RAG Setup Guide endpoint
v1Api.MapPost("/agents/setup", async (SetupGuideRequest req, HttpContext context, SetupGuideService setupGuide, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Setup guide request from user {UserId} for game {GameId}",
        session.User.Id, req.gameId);

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                "Generate setup guide",
                new { endpoint = "setup", gameId = req.gameId },
                ct);
        }

        var resp = await setupGuide.GenerateSetupGuideAsync(req.gameId, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Setup guide delivered for game {GameId}, {StepCount} steps, estimated {Minutes} min",
            req.gameId, resp.steps.Count, resp.estimatedSetupTimeMinutes);

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            var setupSummary = resp.steps.Count > 0
                ? string.Join("; ", resp.steps.Take(3).Select(s => $"{s.stepNumber}. {s.title}")) + (resp.steps.Count > 3 ? "..." : "")
                : "No steps generated";

            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                $"Setup guide for {resp.gameTitle}: {setupSummary}",
                new
                {
                    endpoint = "setup",
                    gameId = req.gameId,
                    gameTitle = resp.gameTitle,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    estimatedSetupTimeMinutes = resp.estimatedSetupTimeMinutes,
                    stepCount = resp.steps.Count,
                    steps = resp.steps
                },
                ct);
        }

        // ADM-01: Log AI request
        var responseSnippet = resp.steps.Count > 0
            ? string.Join("; ", resp.steps.Take(3).Select(s => s.instruction))
            : "No steps generated";
        if (responseSnippet.Length > 500)
        {
            responseSnippet = responseSnippet.Substring(0, 500);
        }

        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "setup",
            "setup_guide",
            responseSnippet,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: null,
            finishReason: null,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to generate setup guide: {ex.Message}",
                    new { endpoint = "setup", gameId = req.gameId, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            req.gameId,
            "setup",
            "setup_guide",
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

v1Api.MapPost("/agents/feedback", async (AgentFeedbackRequest req, HttpContext context, AgentFeedbackService feedbackService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(req.userId, session.User.Id, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "Invalid user" });
    }

    if (string.IsNullOrWhiteSpace(req.messageId) || string.IsNullOrWhiteSpace(req.endpoint))
    {
        return Results.BadRequest(new { error = "messageId and endpoint are required" });
    }

    try
    {
        await feedbackService.RecordFeedbackAsync(
            req.messageId,
            req.endpoint,
            session.User.Id,
            string.IsNullOrWhiteSpace(req.outcome) ? null : req.outcome,
            req.gameId,
            ct);

        logger.LogInformation(
            "Recorded feedback {Outcome} for message {MessageId} on endpoint {Endpoint} by user {UserId}",
            req.outcome ?? "cleared",
            req.messageId,
            req.endpoint,
            session.User.Id);

        return Results.Json(new { ok = true });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to record feedback for message {MessageId}", req.messageId);
        return Results.Problem(detail: "Unable to record feedback", statusCode: 500);
    }
});

// CHESS-04: Chess conversational agent endpoint
v1Api.MapPost("/agents/chess", async (ChessAgentRequest req, HttpContext context, IChessAgentService chessAgent, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(req.question))
    {
        return Results.BadRequest(new { error = "question is required" });
    }

    var startTime = DateTime.UtcNow;
    logger.LogInformation("Chess agent request from user {UserId}: {Question}, FEN: {FEN}",
        session.User.Id, req.question, req.fenPosition ?? "none");

    try
    {
        // Persist user query to chat if chatId provided
        if (req.chatId.HasValue)
        {
            var queryText = !string.IsNullOrWhiteSpace(req.fenPosition)
                ? $"{req.question} [Position: {req.fenPosition}]"
                : req.question;

            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "user",
                queryText,
                new { endpoint = "chess", question = req.question, fenPosition = req.fenPosition },
                ct);
        }

        var resp = await chessAgent.AskAsync(req, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        logger.LogInformation("Chess agent response delivered: {MoveCount} moves suggested",
            resp.suggestedMoves.Count);

        string? model = null;
        string? finishReason = null;
        if (resp.metadata != null)
        {
            if (resp.metadata.TryGetValue("model", out var metadataModel))
            {
                model = metadataModel;
            }

            if (resp.metadata.TryGetValue("finish_reason", out var metadataFinish))
            {
                finishReason = metadataFinish;
            }
        }

        // Persist agent response to chat if chatId provided
        if (req.chatId.HasValue)
        {
            await chatService.AddMessageAsync(
                req.chatId.Value,
                session.User.Id,
                "assistant",
                resp.answer,
                new
                {
                    endpoint = "chess",
                    question = req.question,
                    fenPosition = req.fenPosition,
                    promptTokens = resp.promptTokens,
                    completionTokens = resp.completionTokens,
                    totalTokens = resp.totalTokens,
                    confidence = resp.confidence,
                    model,
                    finishReason,
                    sourceCount = resp.sources.Count,
                    suggestedMoves = resp.suggestedMoves,
                    analysis = resp.analysis
                },
                ct);
        }

        // ADM-01: Log AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            "chess",
            "chess",
            req.question,
            resp.answer?.Length > 500 ? resp.answer.Substring(0, 500) : resp.answer,
            latencyMs,
            resp.totalTokens,
            resp.confidence,
            "Success",
            null,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            promptTokens: resp.promptTokens,
            completionTokens: resp.completionTokens,
            model: model,
            finishReason: finishReason,
            ct: ct);

        return Results.Json(resp);
    }
    catch (Exception ex)
    {
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // Persist error to chat if chatId provided
        if (req.chatId.HasValue)
        {
            try
            {
                await chatService.AddMessageAsync(
                    req.chatId.Value,
                    session.User.Id,
                    "error",
                    $"Failed to process chess question: {ex.Message}",
                    new { endpoint = "chess", question = req.question, fenPosition = req.fenPosition, error = ex.GetType().Name },
                    ct);
            }
            catch (Exception chatEx)
            {
                logger.LogWarning(chatEx, "Failed to log error message to chat {ChatId}", req.chatId.Value);
            }
        }

        // ADM-01: Log failed AI request
        await aiLog.LogRequestAsync(
            session.User.Id,
            "chess",
            "chess",
            req.question,
            null,
            latencyMs,
            status: "Error",
            errorMessage: ex.Message,
            ipAddress: context.Connection.RemoteIpAddress?.ToString(),
            userAgent: context.Request.Headers.UserAgent.ToString(),
            ct: ct);

        throw;
    }
});

v1Api.MapPost("/ingest/pdf", async (HttpContext context, PdfStorageService pdfStorage, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var form = await context.Request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    var gameId = form["gameId"].ToString();

    if (string.IsNullOrWhiteSpace(gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    logger.LogInformation("User {UserId} uploading PDF for game {GameId}", session.User.Id, gameId);

    var result = await pdfStorage.UploadPdfAsync(gameId, session.User.Id, file!, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF upload failed for game {GameId}: {Error}", gameId, result.Message);
        return Results.BadRequest(new { error = result.Message });
    }

    logger.LogInformation("PDF uploaded successfully: {PdfId}", result.Document!.Id);
    return Results.Json(new { documentId = result.Document.Id, fileName = result.Document.FileName });
});

v1Api.MapGet("/games", async (HttpContext context, GameService gameService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
    {
        return Results.Unauthorized();
    }

    var games = await gameService.GetGamesAsync(ct);
    var response = games.Select(g => new GameResponse(g.Id, g.Name, g.CreatedAt)).ToList();
    return Results.Json(response);
});

v1Api.MapPost("/games", async (CreateGameRequest? request, HttpContext context, GameService gameService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning(
            "User {UserId} with role {Role} attempted to create a game without permission",
            session.User.Id,
            session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request is null)
    {
        return Results.BadRequest(new { error = "Request body is required" });
    }

    try
    {
        var game = await gameService.CreateGameAsync(request.Name, request.GameId, ct);
        logger.LogInformation("Created game {GameId}", game.Id);
        return Results.Created($"/games/{game.Id}", new GameResponse(game.Id, game.Name, game.CreatedAt));
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning(ex, "Invalid game creation request");
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Conflict creating game");
        return Results.Conflict(new { error = ex.Message });
    }
});

v1Api.MapGet("/games/{gameId}/pdfs", async (string gameId, HttpContext context, PdfStorageService pdfStorage, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdfs = await pdfStorage.GetPdfsByGameAsync(gameId, ct);
    return Results.Json(new { pdfs });
});

v1Api.MapGet("/pdfs/{pdfId}/text", async (string pdfId, HttpContext context, MeepleAiDbContext db, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new
        {
            p.Id,
            p.FileName,
            p.ExtractedText,
            p.ProcessingStatus,
            p.ProcessedAt,
            p.PageCount,
            p.CharacterCount,
            p.ProcessingError
        })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    return Results.Json(pdf);
});

// SEC-02: Delete PDF with Row-Level Security
v1Api.MapDelete("/pdf/{pdfId}", async (string pdfId, HttpContext context, PdfStorageService pdfStorage, AuditService auditService, MeepleAiDbContext db, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Load PDF to check ownership
    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new { p.Id, p.UploadedByUserId, p.GameId })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    // RLS: Check permissions
    bool isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    bool isOwner = pdf.UploadedByUserId == session.User.Id;

    if (!isAdmin && !isOwner)
    {
        // Audit log access denial
        await auditService.LogAsync(
            session.User.Id,
            "ACCESS_DENIED",
            "PdfDocument",
            pdfId,
            "Denied",
            $"User attempted to delete PDF owned by another user. User role: {session.User.Role}, Owner: {pdf.UploadedByUserId}. RLS scope: own resources only.",
            null,
            null,
            ct);

        logger.LogWarning("User {UserId} with role {Role} denied access to delete PDF {PdfId} (owner: {OwnerId})",
            session.User.Id, session.User.Role, pdfId, pdf.UploadedByUserId);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Delete PDF
    var result = await pdfStorage.DeletePdfAsync(pdfId, ct);

    if (!result.Success)
    {
        logger.LogError("Failed to delete PDF {PdfId}: {Error}", pdfId, result.Message);
        return Results.BadRequest(new { error = result.Message });
    }

    logger.LogInformation("User {UserId} deleted PDF {PdfId}", session.User.Id, pdfId);

    // Audit log successful deletion
    await auditService.LogAsync(
        session.User.Id,
        "DELETE",
        "PdfDocument",
        pdfId,
        "Success",
        $"PDF deleted successfully by user with role: {session.User.Role}",
        null,
        null,
        ct);

    return Results.NoContent();
});

v1Api.MapPost("/ingest/pdf/{pdfId}/rulespec", async (string pdfId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("User {UserId} generating RuleSpec from PDF {PdfId}", session.User.Id, pdfId);
        var ruleSpec = await ruleSpecService.GenerateRuleSpecFromPdfAsync(pdfId, ct);
        return Results.Json(ruleSpec);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Unable to generate RuleSpec for PDF {PdfId}", pdfId);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// AI-01: Index PDF for semantic search
v1Api.MapPost("/ingest/pdf/{pdfId}/index", async (string pdfId, HttpContext context, PdfIndexingService indexingService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index PDF without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("User {UserId} indexing PDF {PdfId}", session.User.Id, pdfId);

    var result = await indexingService.IndexPdfAsync(pdfId, ct);

    if (!result.Success)
    {
        logger.LogWarning("PDF indexing failed for {PdfId}: {Error}", pdfId, result.ErrorMessage);

        return result.ErrorCode switch
        {
            PdfIndexingErrorCode.PdfNotFound => Results.NotFound(new { error = result.ErrorMessage }),
            PdfIndexingErrorCode.TextExtractionRequired => Results.BadRequest(new { error = result.ErrorMessage }),
            _ => Results.BadRequest(new { error = result.ErrorMessage })
        };
    }

    logger.LogInformation("PDF {PdfId} indexed successfully: {ChunkCount} chunks", pdfId, result.ChunkCount);

    return Results.Json(new
    {
        success = true,
        vectorDocumentId = result.VectorDocumentId,
        chunkCount = result.ChunkCount,
        indexedAt = result.IndexedAt
    });
});

v1Api.MapGet("/games/{gameId}/rulespec", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("Fetching RuleSpec for game {GameId}", gameId);
    var ruleSpec = await ruleSpecService.GetRuleSpecAsync(gameId, ct);

    if (ruleSpec == null)
    {
        logger.LogInformation("RuleSpec not found for game {GameId}", gameId);
        return Results.NotFound(new { error = "RuleSpec not found" });
    }

    return Results.Json(ruleSpec);
});

v1Api.MapPut("/games/{gameId}/rulespec", async (string gameId, RuleSpec ruleSpec, HttpContext context, RuleSpecService ruleSpecService, AuditService auditService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to update RuleSpec without permission", session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (!string.Equals(ruleSpec.gameId, gameId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { error = "gameId in URL does not match gameId in RuleSpec" });
    }

    try
    {
        logger.LogInformation("User {UserId} updating RuleSpec for game {GameId}", session.User.Id, gameId);
        var updated = await ruleSpecService.UpdateRuleSpecAsync(gameId, ruleSpec, session.User.Id, ct);
        logger.LogInformation("RuleSpec updated successfully for game {GameId}, version {Version}", gameId, updated.version);

        // Audit trail for RuleSpec changes
        await auditService.LogAsync(
            session.User.Id,
            "UPDATE_RULESPEC",
            "RuleSpec",
            gameId,
            "Success",
            $"Updated RuleSpec to version {updated.version}",
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            ct);

        return Results.Json(updated);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update RuleSpec for game {GameId}: {Error}", gameId, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// RULE-02: Get version history
v1Api.MapGet("/games/{gameId}/rulespec/history", async (string gameId, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version history for game {GameId}", gameId);
    var history = await ruleSpecService.GetVersionHistoryAsync(gameId, ct);
    return Results.Json(history);
});

// RULE-02: Get specific version
v1Api.MapGet("/games/{gameId}/rulespec/versions/{version}", async (string gameId, string version, HttpContext context, RuleSpecService ruleSpecService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Fetching RuleSpec version {Version} for game {GameId}", version, gameId);
    var ruleSpec = await ruleSpecService.GetVersionAsync(gameId, version, ct);

    if (ruleSpec == null)
    {
        logger.LogInformation("RuleSpec version {Version} not found for game {GameId}", version, gameId);
        return Results.NotFound(new { error = "RuleSpec version not found" });
    }

    return Results.Json(ruleSpec);
});

// RULE-02: Compare two versions (diff)
v1Api.MapGet("/games/{gameId}/rulespec/diff", async (string gameId, string? from, string? to, HttpContext context, RuleSpecService ruleSpecService, RuleSpecDiffService diffService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
    {
        return Results.BadRequest(new { error = "Both 'from' and 'to' version parameters are required" });
    }

    logger.LogInformation("Computing diff between versions {FromVersion} and {ToVersion} for game {GameId}", from, to, gameId);

    var fromSpec = await ruleSpecService.GetVersionAsync(gameId, from, ct);
    var toSpec = await ruleSpecService.GetVersionAsync(gameId, to, ct);

    if (fromSpec == null || toSpec == null)
    {
        return Results.NotFound(new { error = "One or both RuleSpec versions not found" });
    }

    var diff = diffService.ComputeDiff(fromSpec, toSpec);
    return Results.Json(diff);
});

// EDIT-02: RuleSpec comment endpoints
v1Api.MapPost("/games/{gameId}/rulespec/versions/{version}/comments", async (string gameId, string version, CreateRuleSpecCommentRequest request, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.CommentText))
    {
        return Results.BadRequest(new { error = "CommentText is required" });
    }

    try
    {
        logger.LogInformation("User {UserId} adding comment to RuleSpec {GameId} version {Version}", session.User.Id, gameId, version);
        var comment = await commentService.AddCommentAsync(gameId, version, request.AtomId, session.User.Id, request.CommentText, ct);
        logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
        return Results.Created($"/api/v1/games/{gameId}/rulespec/comments/{comment.Id}", comment);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to add comment: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

v1Api.MapGet("/games/{gameId}/rulespec/versions/{version}/comments", async (string gameId, string version, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version}", session.User.Id, gameId, version);
    var response = await commentService.GetCommentsForVersionAsync(gameId, version, ct);
    return Results.Json(response);
});

v1Api.MapPut("/games/{gameId}/rulespec/comments/{commentId:guid}", async (string gameId, Guid commentId, UpdateRuleSpecCommentRequest request, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.CommentText))
    {
        return Results.BadRequest(new { error = "CommentText is required" });
    }

    try
    {
        logger.LogInformation("User {UserId} updating comment {CommentId}", session.User.Id, commentId);
        var comment = await commentService.UpdateCommentAsync(commentId, session.User.Id, request.CommentText, ct);
        logger.LogInformation("Comment {CommentId} updated successfully", commentId);
        return Results.Json(comment);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update comment {CommentId}: {Error}", commentId, ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (UnauthorizedAccessException ex)
    {
        logger.LogWarning("User {UserId} not authorized to update comment {CommentId}: {Error}", session.User.Id, commentId, ex.Message);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

v1Api.MapDelete("/games/{gameId}/rulespec/comments/{commentId:guid}", async (string gameId, Guid commentId, HttpContext context, RuleSpecCommentService commentService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

    try
    {
        logger.LogInformation("User {UserId} deleting comment {CommentId}", session.User.Id, commentId);
        var deleted = await commentService.DeleteCommentAsync(commentId, session.User.Id, isAdmin, ct);

        if (!deleted)
        {
            return Results.NotFound(new { error = "Comment not found" });
        }

        logger.LogInformation("Comment {CommentId} deleted successfully", commentId);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException ex)
    {
        logger.LogWarning("User {UserId} not authorized to delete comment {CommentId}: {Error}", session.User.Id, commentId, ex.Message);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

v1Api.MapPost("/admin/seed", async (SeedRequest request, HttpContext context, RuleSpecService rules, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    var spec = await rules.GetOrCreateDemoAsync(request.gameId, ct);
    return Results.Json(new { ok = true, spec });
});

// ADM-01: Admin dashboard endpoints
v1Api.MapGet("/admin/requests", async (HttpContext context, AiRequestLogService logService, int limit = 100, int offset = 0, string? endpoint = null, string? userId = null, string? gameId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await logService.GetRequestsAsync(
        limit,
        offset,
        endpoint,
        userId,
        gameId,
        startDate,
        endDate,
        ct);

    return Results.Json(new { requests = result.Requests, totalCount = result.TotalCount });
});

v1Api.MapGet("/admin/stats", async (HttpContext context, AiRequestLogService logService, AgentFeedbackService feedbackService, DateTime? startDate = null, DateTime? endDate = null, string? userId = null, string? gameId = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var stats = await logService.GetStatsAsync(startDate, endDate, userId, gameId, ct);
    var feedbackStats = await feedbackService.GetStatsAsync(null, userId, gameId, startDate, endDate, ct);

    return Results.Json(new
    {
        stats.TotalRequests,
        stats.AvgLatencyMs,
        stats.TotalTokens,
        stats.SuccessRate,
        stats.EndpointCounts,
        feedbackCounts = feedbackStats.OutcomeCounts,
        totalFeedback = feedbackStats.TotalFeedback,
        feedbackByEndpoint = feedbackStats.EndpointOutcomeCounts
    });
});

// ADM-02: n8n workflow configuration endpoints
v1Api.MapGet("/admin/n8n", async (HttpContext context, N8nConfigService n8nService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var configs = await n8nService.GetConfigsAsync(ct);
    return Results.Json(new { configs });
});

v1Api.MapGet("/admin/n8n/{configId}", async (string configId, HttpContext context, N8nConfigService n8nService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await n8nService.GetConfigAsync(configId, ct);

    if (config == null)
    {
        return Results.NotFound(new { error = "Configuration not found" });
    }

    return Results.Json(config);
});

v1Api.MapPost("/admin/n8n", async (CreateN8nConfigRequest request, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} creating n8n config: {Name}", session.User.Id, request.Name);
        var config = await n8nService.CreateConfigAsync(session.User.Id, request, ct);
        logger.LogInformation("n8n config {ConfigId} created successfully", config.Id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

v1Api.MapPut("/admin/n8n/{configId}", async (string configId, UpdateN8nConfigRequest request, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} updating n8n config {ConfigId}", session.User.Id, configId);
        var config = await n8nService.UpdateConfigAsync(configId, request, ct);
        logger.LogInformation("n8n config {ConfigId} updated successfully", config.Id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

v1Api.MapDelete("/admin/n8n/{configId}", async (string configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting n8n config {ConfigId}", session.User.Id, configId);
    var deleted = await n8nService.DeleteConfigAsync(configId, ct);

    if (!deleted)
    {
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("n8n config {ConfigId} deleted successfully", configId);
    return Results.Json(new { ok = true });
});

v1Api.MapPost("/admin/n8n/{configId}/test", async (string configId, HttpContext context, N8nConfigService n8nService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {UserId} testing n8n config {ConfigId}", session.User.Id, configId);
        var result = await n8nService.TestConnectionAsync(configId, ct);
        logger.LogInformation("n8n config {ConfigId} test result: {Success}", configId, result.Success);
        return Results.Json(result);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to test n8n config: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// AUTH-03: Session management endpoints
v1Api.MapGet("/admin/sessions", async (HttpContext context, ISessionManagementService sessionManagement, int limit = 100, string? userId = null, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var sessions = await sessionManagement.GetAllSessionsAsync(userId, limit, ct);
    return Results.Json(sessions);
});

v1Api.MapDelete("/admin/sessions/{sessionId}", async (string sessionId, HttpContext context, ISessionManagementService sessionManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} revoking session {SessionId}", session.User.Id, sessionId);

    var revoked = await sessionManagement.RevokeSessionAsync(sessionId, ct);
    if (!revoked)
    {
        return Results.NotFound(new { error = "Session not found or already revoked" });
    }

    logger.LogInformation("Session {SessionId} revoked successfully", sessionId);
    return Results.Json(new { ok = true });
});

v1Api.MapDelete("/admin/users/{userId}/sessions", async (string userId, HttpContext context, ISessionManagementService sessionManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} revoking all sessions for user {UserId}", session.User.Id, userId);

    var count = await sessionManagement.RevokeAllUserSessionsAsync(userId, ct);

    logger.LogInformation("Revoked {Count} sessions for user {UserId}", count, userId);
    return Results.Json(new { ok = true, revokedCount = count });
});

// API-04: Admin API Key Management endpoint
v1Api.MapDelete("/admin/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to delete API key without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} permanently deleting API key {KeyId}", session.User.Id, keyId);

    var success = await apiKeyManagement.DeleteApiKeyAsync(keyId, session.User.Id, ct);

    if (!success)
    {
        logger.LogWarning("API key {KeyId} not found for admin deletion", keyId);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {KeyId} permanently deleted by admin {AdminId}", keyId, session.User.Id);
    return Results.NoContent();
});

v1Api.MapGet("/users/me/sessions", async (HttpContext context, ISessionManagementService sessionManagement, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var sessions = await sessionManagement.GetUserSessionsAsync(session.User.Id, ct);
    return Results.Json(sessions);
});

// AI-07: Prompt versioning and management endpoints

// Create prompt template (Admin only)
v1Api.MapPost("/prompts", async (CreatePromptTemplateRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to create prompt template without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} creating prompt template '{TemplateName}'", session.User.Id, request.Name);
        var response = await promptManagement.CreatePromptTemplateAsync(request, session.User.Id, ct);
        logger.LogInformation("Prompt template {TemplateId} created successfully", response.Template.Id);
        return Results.Created($"/api/v1/prompts/{response.Template.Id}", response);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create prompt template: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid prompt template request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Create new version of prompt template (Admin only)
v1Api.MapPost("/prompts/{templateId}/versions", async (string templateId, CreatePromptVersionRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to create prompt version without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} creating new version for prompt template {TemplateId}", session.User.Id, templateId);
        var version = await promptManagement.CreatePromptVersionAsync(templateId, request, session.User.Id, ct);
        logger.LogInformation("Prompt version {VersionId} (v{VersionNumber}) created successfully", version.Id, version.VersionNumber);
        return Results.Created($"/api/v1/prompts/{templateId}/versions/{version.VersionNumber}", version);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create prompt version: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid prompt version request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Get version history for prompt template (Admin only)
v1Api.MapGet("/prompts/{templateId}/versions", async (string templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to view prompt version history without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var history = await promptManagement.GetVersionHistoryAsync(templateId, ct);
        return Results.Json(history);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to get version history: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
});

// Get active version of prompt template (Authenticated users)
v1Api.MapGet("/prompts/{templateId}/versions/active", async (string templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // Get template to retrieve name
    var template = await promptManagement.GetTemplateAsync(templateId, ct);
    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    var activeVersion = await promptManagement.GetActiveVersionAsync(template.Name, ct);
    if (activeVersion == null)
    {
        return Results.NotFound(new { error = "No active version found for this template" });
    }

    return Results.Json(activeVersion);
});

// Activate version (rollback capability) (Admin only)
v1Api.MapPut("/prompts/{templateId}/versions/{versionId}/activate", async (string templateId, string versionId, ActivatePromptVersionRequest request, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to activate prompt version without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        logger.LogInformation("Admin {AdminId} activating version {VersionId} for template {TemplateId}", session.User.Id, versionId, templateId);
        var activatedVersion = await promptManagement.ActivateVersionAsync(templateId, versionId, session.User.Id, request.Reason, ct);
        logger.LogInformation("Version {VersionId} (v{VersionNumber}) activated successfully", activatedVersion.Id, activatedVersion.VersionNumber);
        return Results.Json(activatedVersion);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to activate prompt version: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid activation request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// Get audit log for prompt template (Admin only)
v1Api.MapGet("/prompts/{templateId}/audit-log", async (string templateId, int limit, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to view audit log without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    try
    {
        var auditLog = await promptManagement.GetAuditLogAsync(templateId, limit, ct);
        return Results.Json(auditLog);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to get audit log: {Error}", ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid audit log request: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// List all prompt templates (Admin only)
v1Api.MapGet("/prompts", async (string? category, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to list prompt templates without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var templates = await promptManagement.ListTemplatesAsync(category, ct);
    return Results.Json(templates);
});

// Get specific prompt template (Admin only)
v1Api.MapGet("/prompts/{templateId}", async (string templateId, HttpContext context, IPromptManagementService promptManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to get prompt template without admin permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var template = await promptManagement.GetTemplateAsync(templateId, ct);
    if (template == null)
    {
        return Results.NotFound(new { error = "Template not found" });
    }

    return Results.Json(template);
});

// API-04: API Key Management endpoints
v1Api.MapPost("/api-keys", async (CreateApiKeyRequest request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.KeyName))
    {
        return Results.BadRequest(new { error = "Key name is required" });
    }

    logger.LogInformation("User {UserId} creating API key '{KeyName}'", session.User.Id, request.KeyName);

    try
    {
        var result = await apiKeyManagement.CreateApiKeyAsync(
            session.User.Id,
            request,
            ct);

        logger.LogInformation("API key '{KeyId}' created for user {UserId}", result.ApiKey.Id, session.User.Id);

        return Results.Created($"/api/v1/api-keys/{result.ApiKey.Id}", result);
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Invalid API key creation request from user {UserId}: {Error}", session.User.Id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
});

v1Api.MapGet("/api-keys", async (HttpContext context, ApiKeyManagementService apiKeyManagement, bool includeRevoked = false, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var result = await apiKeyManagement.ListApiKeysAsync(session.User.Id, includeRevoked, page, pageSize, ct);
    return Results.Json(result);
});

v1Api.MapGet("/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var apiKey = await apiKeyManagement.GetApiKeyAsync(keyId, session.User.Id, ct);

    if (apiKey == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    return Results.Json(apiKey);
});

v1Api.MapPut("/api-keys/{keyId}", async (string keyId, UpdateApiKeyRequest request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} updating API key {KeyId}", session.User.Id, keyId);

    var updated = await apiKeyManagement.UpdateApiKeyAsync(
        keyId,
        session.User.Id,
        request,
        ct);

    if (updated == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {KeyId} updated by user {UserId}", keyId, session.User.Id);
    return Results.Json(updated);
});

v1Api.MapDelete("/api-keys/{keyId}", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} revoking API key {KeyId}", session.User.Id, keyId);

    var success = await apiKeyManagement.RevokeApiKeyAsync(keyId, session.User.Id, ct);

    if (!success)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {KeyId} revoked by user {UserId}", keyId, session.User.Id);
    return Results.NoContent();
});

v1Api.MapPost("/api-keys/{keyId}/rotate", async (string keyId, RotateApiKeyRequest? request, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    logger.LogInformation("User {UserId} rotating API key {KeyId}", session.User.Id, keyId);

    var result = await apiKeyManagement.RotateApiKeyAsync(
        keyId,
        session.User.Id,
        request ?? new RotateApiKeyRequest(),
        ct);

    if (result == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    logger.LogInformation("API key {OldKeyId} rotated to {NewKeyId} by user {UserId}", keyId, result.NewApiKey.Id, session.User.Id);
    return Results.Json(result);
});

v1Api.MapGet("/api-keys/{keyId}/usage", async (string keyId, HttpContext context, ApiKeyManagementService apiKeyManagement, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var usage = await apiKeyManagement.GetApiKeyUsageAsync(keyId, session.User.Id, ct);

    if (usage == null)
    {
        logger.LogWarning("API key {KeyId} not found for user {UserId}", keyId, session.User.Id);
        return Results.NotFound(new { error = "API key not found" });
    }

    return Results.Json(usage);
});

// CHESS-03: Chess knowledge indexing endpoints
v1Api.MapPost("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to index chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} starting chess knowledge indexing", session.User.Id);

    var result = await chessService.IndexChessKnowledgeAsync(ct);

    if (!result.Success)
    {
        logger.LogError("Chess knowledge indexing failed: {Error}", result.ErrorMessage);
        return Results.BadRequest(new { error = result.ErrorMessage });
    }

    logger.LogInformation("Chess knowledge indexing completed: {TotalItems} items, {TotalChunks} chunks",
        result.TotalKnowledgeItems, result.TotalChunks);

    return Results.Json(new
    {
        success = true,
        totalItems = result.TotalKnowledgeItems,
        totalChunks = result.TotalChunks,
        categoryCounts = result.CategoryCounts
    });
});

v1Api.MapGet("/chess/search", async (string? q, int? limit, HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(q))
    {
        return Results.BadRequest(new { error = "Query parameter 'q' is required" });
    }

    logger.LogInformation("User {UserId} searching chess knowledge: {Query}", session.User.Id, q);

    var searchResult = await chessService.SearchChessKnowledgeAsync(q, limit ?? 5, ct);

    if (!searchResult.Success)
    {
        logger.LogError("Chess knowledge search failed: {Error}", searchResult.ErrorMessage);
        return Results.BadRequest(new { error = searchResult.ErrorMessage });
    }

    logger.LogInformation("Chess knowledge search completed: {ResultCount} results", searchResult.Results.Count);

    return Results.Json(new
    {
        success = true,
        results = searchResult.Results.Select(r => new
        {
            score = r.Score,
            text = r.Text,
            page = r.Page,
            chunkIndex = r.ChunkIndex
        })
    });
});

v1Api.MapDelete("/chess/index", async (HttpContext context, IChessKnowledgeService chessService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to delete chess knowledge without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {UserId} deleting all chess knowledge", session.User.Id);

    var success = await chessService.DeleteChessKnowledgeAsync(ct);

    if (!success)
    {
        logger.LogError("Chess knowledge deletion failed");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    logger.LogInformation("Chess knowledge deletion completed successfully");
    return Results.Json(new { success = true });
});

// UI-01: Chat management endpoints
v1Api.MapGet("/chats", async (HttpContext context, ChatService chatService, string? gameId, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var chats = string.IsNullOrWhiteSpace(gameId)
        ? await chatService.GetUserChatsAsync(session.User.Id, 50, ct)
        : await chatService.GetUserChatsByGameAsync(session.User.Id, gameId, 50, ct);

    var response = chats.Select(c => new ChatDto(
        c.Id,
        c.GameId,
        c.Game.Name,
        c.AgentId,
        c.Agent.Name,
        c.StartedAt,
        c.LastMessageAt
    )).ToList();

    return Results.Json(response);
});

v1Api.MapGet("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var chat = await chatService.GetChatByIdAsync(chatId, session.User.Id, ct);
    if (chat == null)
    {
        return Results.NotFound(new { error = "Chat not found" });
    }

    var messages = chat.Logs.Select(l => new ChatMessageDto(
        l.Id,
        l.Level,
        l.Message,
        l.MetadataJson,
        l.CreatedAt
    )).ToList();

    var response = new ChatWithHistoryDto(
        chat.Id,
        chat.GameId,
        chat.Game.Name,
        chat.AgentId,
        chat.Agent.Name,
        chat.StartedAt,
        chat.LastMessageAt,
        messages
    );

    return Results.Json(response);
});

v1Api.MapPost("/chats", async (CreateChatRequest? request, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (request == null)
    {
        return Results.BadRequest(new { error = "Request body is required" });
    }

    if (string.IsNullOrWhiteSpace(request.GameId) || string.IsNullOrWhiteSpace(request.AgentId))
    {
        return Results.BadRequest(new { error = "GameId and AgentId are required" });
    }

    try
    {
        var chat = await chatService.CreateChatAsync(session.User.Id, request.GameId, request.AgentId, ct);

        // Reload with navigations
        var fullChat = await chatService.GetChatByIdAsync(chat.Id, session.User.Id, ct);
        if (fullChat == null)
        {
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var response = new ChatDto(
            fullChat.Id,
            fullChat.GameId,
            fullChat.Game.Name,
            fullChat.AgentId,
            fullChat.Agent.Name,
            fullChat.StartedAt,
            fullChat.LastMessageAt
        );

        logger.LogInformation("User {UserId} created chat {ChatId} for game {GameId}", session.User.Id, chat.Id, request.GameId);
        return Results.Created($"/chats/{chat.Id}", response);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(ex, "Invalid chat creation request");
        return Results.BadRequest(new { error = ex.Message });
    }
});

v1Api.MapDelete("/chats/{chatId:guid}", async (Guid chatId, HttpContext context, ChatService chatService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    try
    {
        var deleted = await chatService.DeleteChatAsync(chatId, session.User.Id, ct);
        if (!deleted)
        {
            return Results.NotFound(new { error = "Chat not found" });
        }

        logger.LogInformation("User {UserId} deleted chat {ChatId}", session.User.Id, chatId);
        return Results.NoContent();
    }
    catch (UnauthorizedAccessException ex)
    {
        logger.LogWarning(ex, "Unauthorized chat deletion attempt");
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

v1Api.MapGet("/games/{gameId}/agents", async (string gameId, HttpContext context, ChatService chatService, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession)
    {
        return Results.Unauthorized();
    }

    var agents = await chatService.GetAgentsForGameAsync(gameId, ct);
    var response = agents.Select(a => new AgentDto(
        a.Id,
        a.GameId,
        a.Name,
        a.Kind,
        a.CreatedAt
    )).ToList();

    return Results.Json(response);
});

app.Run();

static CookieOptions CreateSessionCookieOptions(HttpContext context, DateTime expiresAt)
{
    var options = BuildSessionCookieOptions(context);
    options.Expires = expiresAt;
    return options;
}

static void WriteSessionCookie(HttpContext context, string token, DateTime expiresAt)
{
    var options = CreateSessionCookieOptions(context, expiresAt);
    var sessionCookieName = GetSessionCookieName(context);
    context.Response.Cookies.Append(sessionCookieName, token, options);
}

static void RemoveSessionCookie(HttpContext context)
{
    var options = BuildSessionCookieOptions(context);
    options.Expires = DateTimeOffset.UnixEpoch;

    var sessionCookieName = GetSessionCookieName(context);
    context.Response.Cookies.Delete(sessionCookieName, options);
}

static CookieOptions BuildSessionCookieOptions(HttpContext context)
{
    var configuration = GetSessionCookieConfiguration(context);

    var isHttps = context.Request.IsHttps;

    if (!isHttps && configuration.UseForwardedProto &&
        context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
    {
        foreach (var proto in forwardedProto)
        {
            if (string.Equals(proto, "https", StringComparison.OrdinalIgnoreCase))
            {
                isHttps = true;
                break;
            }
        }
    }

    var secure = configuration.Secure ?? isHttps;
    var secureForced = false;

    if (!secure && !configuration.Secure.HasValue)
    {
        secure = true;
        secureForced = true;
    }

    var sameSite = configuration.SameSite ?? (secure ? SameSiteMode.None : SameSiteMode.Lax);

    if (secureForced && sameSite != SameSiteMode.None)
    {
        sameSite = SameSiteMode.None;
    }
    var path = string.IsNullOrWhiteSpace(configuration.Path) ? "/" : configuration.Path;

    var options = new CookieOptions
    {
        HttpOnly = configuration.HttpOnly,
        Secure = secure,
        SameSite = sameSite,
        Path = path
    };

    if (!string.IsNullOrWhiteSpace(configuration.Domain))
    {
        options.Domain = configuration.Domain;
    }

    return options;
}

static string GetSessionCookieName(HttpContext context)
{
    var configuration = GetSessionCookieConfiguration(context);
    return string.IsNullOrWhiteSpace(configuration.Name)
        ? AuthService.SessionCookieName
        : configuration.Name;
}

static SessionCookieConfiguration GetSessionCookieConfiguration(HttpContext context)
{
    return context.RequestServices
        .GetRequiredService<IOptions<SessionCookieConfiguration>>()
        .Value;
}
public partial class Program { }
