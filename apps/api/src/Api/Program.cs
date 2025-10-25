using Api.Infrastructure;
using System;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Api.Infrastructure.Entities;
using Api.Middleware;
using Api.Models;
using Api.Services;
using Api.Services.Chat; // CHAT-02
using Api.Configuration; // CHAT-02
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc; // EDIT-05: For [FromServices] attribute
using Microsoft.AspNetCore.ResponseCompression; // PERF-11: Response compression
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
builder.Services.Configure<PdfProcessingConfiguration>(builder.Configuration.GetSection("PdfProcessing"));
builder.Services.Configure<FollowUpQuestionsConfiguration>(builder.Configuration.GetSection("FollowUpQuestions")); // CHAT-02
builder.Services.Configure<RagPromptsConfiguration>(builder.Configuration.GetSection("RagPrompts")); // AI-07.1: RAG prompt templates
builder.Services.Configure<HybridCacheConfiguration>(builder.Configuration.GetSection("HybridCache")); // PERF-05: HybridCache configuration

// Only configure Postgres in non-test environments (tests will override with SQLite)
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? builder.Configuration["ConnectionStrings__Postgres"]
        ?? throw new InvalidOperationException("Missing Postgres connection string");

    // PERF-09: Optimize Postgres connection pooling for better throughput
    builder.Services.AddDbContext<MeepleAiDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            // Connection resilience
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);

            // Command timeout (30 seconds for complex queries)
            npgsqlOptions.CommandTimeout(30);

            // Batch size for bulk operations
            npgsqlOptions.MaxBatchSize(100);
        });

        // Performance optimizations
        options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
        options.EnableDetailedErrors(builder.Environment.IsDevelopment());

        // Query behavior
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // PERF-06: Default to no-tracking
    });
}

// PERF-09: Configure Redis with optimized connection pooling
var redisUrl = builder.Configuration["REDIS_URL"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisUrl);

    // Connection resilience
    configuration.AbortOnConnectFail = false; // Fail gracefully if Redis unavailable
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000; // 5 seconds
    configuration.SyncTimeout = 5000; // 5 seconds

    // Performance optimizations
    configuration.KeepAlive = 60; // Keep-alive every 60 seconds
    configuration.AllowAdmin = false; // Disable admin commands for security

    // Connection pooling (StackExchange.Redis manages pool internally)
    configuration.DefaultDatabase = 0;

    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Connecting to Redis at {RedisUrl} with optimized settings", redisUrl);

    return ConnectionMultiplexer.Connect(configuration);
});

// PERF-05: Configure HybridCache with conditional L1 (in-memory) + L2 (Redis) support
var hybridCacheConfig = builder.Configuration.GetSection("HybridCache").Get<HybridCacheConfiguration>()
    ?? new HybridCacheConfiguration();

#pragma warning disable EXTEXP0018 // HybridCache preview APIs
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = hybridCacheConfig.MaximumPayloadBytes;
    options.DefaultEntryOptions = new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
    {
        Expiration = hybridCacheConfig.DefaultExpiration,
        LocalCacheExpiration = hybridCacheConfig.DefaultExpiration
    };
});
#pragma warning restore EXTEXP0018

// Add L2 distributed cache (Redis) if enabled
if (hybridCacheConfig.EnableL2Cache)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisUrl;
        options.InstanceName = "meepleai:hybridcache:";
    });
}

// Register HybridCache service wrapper
builder.Services.AddScoped<IHybridCacheService, HybridCacheService>();

// PERF-09: Configure HttpClient with connection pooling optimizations
builder.Services.AddHttpClient(); // Default client with pooling

// Ollama client with optimized settings
builder.Services.AddHttpClient("Ollama", client =>
{
    var ollamaUrl = builder.Configuration["OLLAMA_URL"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2), // Recycle connections every 2 min
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1), // Close idle connections after 1 min
    MaxConnectionsPerServer = 20, // Allow up to 20 concurrent connections
    EnableMultipleHttp2Connections = true // Enable HTTP/2 multiplexing
});

// OpenAI client with optimized settings
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 10,
    EnableMultipleHttp2Connections = true
});

// OpenRouter client with optimized settings
builder.Services.AddHttpClient("OpenRouter", client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 10,
    EnableMultipleHttp2Connections = true
});

// Qdrant client with optimized settings
builder.Services.AddHttpClient("Qdrant", client =>
{
    var qdrantUrl = builder.Configuration["QdrantUrl"] ?? "http://localhost:6333";
    client.BaseAddress = new Uri(qdrantUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
    MaxConnectionsPerServer = 30, // Higher for vector DB operations
    EnableMultipleHttp2Connections = true
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

// AI-09: Language detection for multi-language support
builder.Services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();

// AI-05: AI response caching
// PERF-03: Changed from Singleton to Scoped due to MeepleAiDbContext dependency
builder.Services.AddScoped<IAiResponseCacheService, AiResponseCacheService>();

// AI-10: Cache optimization services
builder.Services.Configure<CacheOptimizationConfiguration>(
    builder.Configuration.GetSection("CacheOptimization"));
builder.Services.AddSingleton<ICacheMetricsRecorder, CacheMetricsRecorder>();
builder.Services.AddSingleton<IDynamicTtlStrategy, DynamicTtlStrategy>();
builder.Services.AddSingleton<IRedisFrequencyTracker, RedisFrequencyTracker>();

// AI-10: Conditional cache warming service (disabled by default)
var cacheWarmingEnabled = builder.Configuration
    .GetSection("CacheOptimization:WarmingEnabled")
    .Get<bool>();
if (cacheWarmingEnabled)
{
    builder.Services.AddHostedService<CacheWarmingService>();
}

// PERF-02: Session caching (Phase 2 optimization)
builder.Services.AddSingleton<ISessionCacheService, SessionCacheService>();

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<RuleSpecService>();
builder.Services.AddScoped<RuleSpecDiffService>();
builder.Services.AddScoped<RuleCommentService>(); // EDIT-05: Comment service with threading and mentions (concrete registration for Minimal API compatibility)
builder.Services.AddScoped<IRagService, RagService>(); // AI-04: RAG service for Q&A and explanations
builder.Services.AddScoped<IStreamingRagService, StreamingRagService>(); // API-02: Streaming RAG service
builder.Services.AddScoped<IStreamingQaService, StreamingQaService>(); // CHAT-01: Streaming QA service
builder.Services.AddScoped<IFollowUpQuestionService, FollowUpQuestionService>(); // CHAT-02: Follow-up question generation
builder.Services.AddScoped<SetupGuideService>();
builder.Services.AddScoped<IPromptTemplateService, PromptTemplateService>(); // AI-07.1: Prompt template service with few-shot learning
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AiRequestLogService>();
builder.Services.AddScoped<AgentFeedbackService>();
builder.Services.AddScoped<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<PdfTextExtractionService>();
builder.Services.AddScoped<PdfTableExtractionService>();
builder.Services.AddScoped<IPdfValidationService, PdfValidationService>(); // PDF-09: PDF validation service
builder.Services.AddScoped<PdfStorageService>();
builder.Services.AddScoped<N8nConfigService>();
builder.Services.AddScoped<ChatService>();

// CHAT-05: Chat export services
builder.Services.AddScoped<IChatExportService, ChatExportService>();
builder.Services.AddScoped<IExportFormatter, TxtExportFormatter>();
builder.Services.AddScoped<IExportFormatter, MdExportFormatter>();
builder.Services.AddScoped<IExportFormatter, PdfExportFormatter>();

// AUTH-03: Session management service
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddHostedService<SessionAutoRevocationService>();

// ADMIN-01: User management service
builder.Services.AddScoped<UserManagementService>();

// ADMIN-02: Analytics dashboard service
builder.Services.AddScoped<IAdminStatsService, AdminStatsService>();

// AUTH-04: Password reset services
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailService, EmailService>();

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

// AI-11: Quality tracking services
builder.Services.AddScoped<IResponseQualityService, ResponseQualityService>();
builder.Services.AddSingleton<QualityMetrics>();
builder.Services.AddSingleton<QualityReportService>();
builder.Services.AddSingleton<IQualityReportService>(sp => sp.GetRequiredService<QualityReportService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<QualityReportService>());

// CONFIG-01: Dynamic configuration service
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

// CONFIG-05: Feature flags service
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();

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

// PERF-11: Enable Response Compression (must be early in pipeline)
app.UseResponseCompression();

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
    var rateLimiter = context.RequestServices.GetRequiredService<IRateLimitService>();
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

// AUTH-04: Password reset endpoints
v1Api.MapPost("/auth/password-reset/request", async (
    PasswordResetRequestPayload payload,
    IPasswordResetService passwordResetService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return Results.BadRequest(new { error = "Email is required" });
        }

        await passwordResetService.RequestPasswordResetAsync(payload.Email, ct);

        // Always return success to prevent email enumeration
        return Results.Json(new { ok = true, message = "If the email exists, a password reset link has been sent" });
    }
    catch (InvalidOperationException ex)
    {
        // Rate limit or validation errors
        logger.LogWarning("Password reset request error: {Message}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Password reset request endpoint error");
        return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
    }
});

v1Api.MapGet("/auth/password-reset/verify", async (
    string token,
    IPasswordResetService passwordResetService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest(new { error = "Token is required" });
        }

        var isValid = await passwordResetService.ValidateResetTokenAsync(token, ct);

        if (!isValid)
        {
            return Results.BadRequest(new { error = "Invalid or expired token" });
        }

        return Results.Json(new { ok = true, message = "Token is valid" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Password reset verify endpoint error");
        return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
    }
});

v1Api.MapPut("/auth/password-reset/confirm", async (
    PasswordResetConfirmPayload payload,
    HttpContext context,
    IPasswordResetService passwordResetService,
    AuthService authService,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(payload.Token))
        {
            return Results.BadRequest(new { error = "Token is required" });
        }

        if (string.IsNullOrWhiteSpace(payload.NewPassword))
        {
            return Results.BadRequest(new { error = "New password is required" });
        }

        // Tuple destructuring for userId
        var (success, userId) = await passwordResetService.ResetPasswordAsync(
            payload.Token,
            payload.NewPassword,
            ct);

        if (!success || userId == null)
        {
            return Results.NotFound(new { error = "Invalid or expired token" });
        }

        // Create new session for auto-login
        var sessionResult = await authService.CreateSessionForUserAsync(
            userId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            ct);

        if (sessionResult != null)
        {
            WriteSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);
        }

        return Results.Json(new { ok = true, message = "Password has been reset successfully" });
    }
    catch (ArgumentException ex)
    {
        // Validation errors (password complexity, etc.)
        logger.LogWarning("Password reset confirm validation error: {Message}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Password reset confirm endpoint error");
        return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
    }
});

// API-01: AI agent endpoints (versioned)
v1Api.MapPost("/agents/qa", async (
    QaRequest req,
    HttpContext context,
    IRagService rag,
    ChatService chatService,
    AiRequestLogService aiLog,
    IResponseQualityService qualityService, // AI-11: Quality scoring
    IFollowUpQuestionService followUpService, // CHAT-02
    IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
    MeepleAiDbContext dbContext, // CHAT-02: for game name lookup
    ILogger<Program> logger,
    bool bypassCache = false,
    bool generateFollowUps = true, // CHAT-02: opt-in parameter
    CancellationToken ct = default) =>
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
    var config = followUpConfig.Value;
    generateFollowUps = generateFollowUps && config.Enabled; // Apply global feature flag

    logger.LogInformation("QA request from user {UserId} for game {GameId}: {Query} (bypassCache: {BypassCache}, generateFollowUps: {GenerateFollowUps})",
        session.User.Id, req.gameId, req.query, bypassCache, generateFollowUps);

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
                new { endpoint = "qa", gameId = req.gameId, bypassCache, generateFollowUps },
                ct);
        }

        // PERF-03: Support cache bypass via query parameter
        // AI-09: Language parameter defaults to null (uses "en")
        var resp = await rag.AskAsync(req.gameId, req.query, language: null, bypassCache, ct);
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // CHAT-02: Generate follow-up questions if enabled
        IReadOnlyList<string>? followUpQuestions = null;
        if (generateFollowUps)
        {
            var game = await dbContext.Games
                .Where(g => g.Id == req.gameId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(ct);

            if (game != null)
            {
                followUpQuestions = await followUpService.GenerateQuestionsAsync(
                    originalQuestion: req.query,
                    generatedAnswer: resp.answer,
                    ragContext: resp.snippets,
                    gameName: game,
                    ct: ct);

                logger.LogInformation("Generated {Count} follow-up questions for game {GameId}",
                    followUpQuestions.Count, req.gameId);
            }
        }

        // Create response with follow-up questions
        var finalResponse = new QaResponse(
            resp.answer,
            resp.snippets,
            resp.promptTokens,
            resp.completionTokens,
            resp.totalTokens,
            resp.confidence,
            resp.metadata,
            followUpQuestions); // CHAT-02

        logger.LogInformation("QA response delivered for game {GameId}", req.gameId);

        // AI-11: Calculate quality scores from response data using actual RAG scores
        var ragSearchResults = resp.snippets.Select(s => new RagSearchResult { Score = s.score }).ToList();
        var citations = resp.snippets.Select(s => new Citation
        {
            DocumentId = Guid.NewGuid(), // Placeholder - snippets don't have document IDs
            PageNumber = s.page,
            SnippetText = s.text
        }).ToList();
        var qualityScores = qualityService.CalculateQualityScores(
            ragSearchResults,
            citations,
            resp.answer,
            null); // Let service calculate LLM confidence from response text (resp.confidence is RAG max, not LLM confidence)

        // AI-11: Debug logging for quality scores
        logger.LogInformation(
            "Quality scores calculated - RAG: {RagConf:F3}, LLM: {LlmConf:F3}, Citation: {CitConf:F3}, Overall: {OverallConf:F3}, IsLowQuality: {IsLowQuality}, SnippetScores: [{Scores}]",
            qualityScores.RagConfidence,
            qualityScores.LlmConfidence,
            qualityScores.CitationQuality,
            qualityScores.OverallConfidence,
            qualityScores.IsLowQuality,
            string.Join(", ", ragSearchResults.Select(r => r.Score.ToString("F3"))));

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
                    snippetCount = resp.snippets.Count,
                    followUpQuestionsCount = followUpQuestions?.Count ?? 0 // CHAT-02
                },
                ct);
        }

        // ADM-01: Log AI request with AI-11 quality scores
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
            qualityScores: qualityScores, // AI-11: Include quality scores
            ct: ct);

        return Results.Json(finalResponse); // CHAT-02: Return response with follow-up questions
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
})
.WithName("QaAgent")
.WithDescription("Ask a question about game rules using RAG (Retrieval-Augmented Generation)")
.WithTags("AI Agents")
.Produces<QaResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status500InternalServerError);

v1Api.MapPost("/agents/explain", async (ExplainRequest req, HttpContext context, IRagService rag, ChatService chatService, AiRequestLogService aiLog, ILogger<Program> logger, CancellationToken ct) =>
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

        // AI-09: Language parameter defaults to null (uses "en")
        var resp = await rag.ExplainAsync(req.gameId, req.topic, language: null, ct);
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
v1Api.MapPost("/agents/qa/stream", async (
    QaRequest req,
    HttpContext context,
    IStreamingQaService streamingQa,
    ChatService chatService,
    AiRequestLogService aiLog,
    IFollowUpQuestionService followUpService, // CHAT-02
    IOptions<FollowUpQuestionsConfiguration> followUpConfig, // CHAT-02
    MeepleAiDbContext dbContext, // CHAT-02
    IFeatureFlagService featureFlags, // CONFIG-05
    ILogger<Program> logger,
    bool generateFollowUps = true, // CHAT-02: opt-in parameter
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if streaming responses feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.StreamingResponses"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "Streaming responses are currently disabled", featureName = "Features.StreamingResponses" },
            statusCode: 403);
    }

    if (string.IsNullOrWhiteSpace(req.gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    // CHAT-02: Apply global feature flag for follow-up questions
    var config = followUpConfig.Value;
    generateFollowUps = generateFollowUps && config.Enabled;

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

        // CHAT-02: Follow-up question generation (fire-and-forget after Complete event)
        Task<IReadOnlyList<string>>? followUpTask = null;
        IReadOnlyList<string>? followUpQuestions = null;
        string? gameName = null;

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

                // CHAT-02: Start follow-up generation in parallel (fire-and-forget)
                if (generateFollowUps && followUpTask == null)
                {
                    followUpTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Fetch game name
                            gameName = await dbContext.Games
                                .Where(g => g.Id.ToString() == req.gameId)
                                .Select(g => g.Name)
                                .FirstOrDefaultAsync(ct);

                            if (gameName == null)
                            {
                                logger.LogWarning("Game {GameId} not found for follow-up generation", req.gameId);
                                return new List<string>().AsReadOnly();
                            }

                            var answer = answerBuilder.ToString();
                            return await followUpService.GenerateQuestionsAsync(
                                originalQuestion: req.query,
                                generatedAnswer: answer,
                                ragContext: snippets,
                                gameName: gameName,
                                ct: ct);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to generate follow-up questions for game {GameId}", req.gameId);
                            return new List<string>().AsReadOnly();
                        }
                    });
                }
            }
        }

        var answer = answerBuilder.ToString();
        var latencyMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

        // CHAT-02: Wait for follow-up questions and send event
        if (followUpTask != null)
        {
            try
            {
                followUpQuestions = await followUpTask;
                if (followUpQuestions != null && followUpQuestions.Count > 0)
                {
                    var followUpEvent = new RagStreamingEvent(
                        StreamingEventType.FollowUpQuestions,
                        new StreamingFollowUpQuestions(followUpQuestions),
                        DateTime.UtcNow);
                    var followUpJson = System.Text.Json.JsonSerializer.Serialize(followUpEvent);
                    await context.Response.WriteAsync($"data: {followUpJson}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);

                    logger.LogInformation("Sent {Count} follow-up questions for game {GameId}",
                        followUpQuestions.Count, req.gameId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send follow-up questions event for game {GameId}", req.gameId);
            }
        }

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
                    snippetCount = snippets.Count,
                    followUpQuestionsCount = followUpQuestions?.Count ?? 0 // CHAT-02
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
v1Api.MapPost("/agents/setup", async (SetupGuideRequest req, HttpContext context, SetupGuideService setupGuide, ChatService chatService, AiRequestLogService aiLog, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if setup guide generation feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.SetupGuideGeneration"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "Setup guide generation is currently unavailable", featureName = "Features.SetupGuideGeneration" },
            statusCode: 403);
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

v1Api.MapPost("/ingest/pdf", async (HttpContext context, IPdfValidationService pdfValidation, PdfStorageService pdfStorage, IFeatureFlagService featureFlags, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if PDF upload feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.PdfUpload"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "PDF uploads are currently disabled", featureName = "Features.PdfUpload" },
            statusCode: 403);
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

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string> { ["file"] = "No file provided" } });
    }

    logger.LogInformation("User {UserId} uploading PDF for game {GameId}", session.User.Id, gameId);

    // PDF-09: Validate file size
    var sizeValidation = pdfValidation.ValidateFileSize(file.Length);
    if (!sizeValidation.IsValid)
    {
        logger.LogWarning("PDF validation failed for {FileName}: File size validation failed", file.FileName);
        return Results.BadRequest(new { error = "validation_failed", details = sizeValidation.Errors });
    }

    // PDF-09: Validate MIME type
    var mimeValidation = pdfValidation.ValidateMimeType(file.ContentType);
    if (!mimeValidation.IsValid)
    {
        logger.LogWarning("PDF validation failed for {FileName}: MIME type validation failed", file.FileName);
        return Results.BadRequest(new { error = "validation_failed", details = mimeValidation.Errors });
    }

    // PDF-09: Deep validation with PDF content
    using var stream = file.OpenReadStream();
    var validation = await pdfValidation.ValidateAsync(stream, file.FileName, ct);

    if (!validation.IsValid)
    {
        logger.LogWarning("PDF validation failed for {FileName}: {@Errors}", file.FileName, validation.Errors);
        return Results.BadRequest(new { error = "validation_failed", details = validation.Errors });
    }

    // Reset stream position for processing
    stream.Position = 0;

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

// PDF-08: Get PDF processing progress
v1Api.MapGet("/pdfs/{pdfId}/progress", async (string pdfId, HttpContext context, MeepleAiDbContext db, CancellationToken ct) =>
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
            p.UploadedByUserId,
            p.ProcessingProgressJson
        })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    // Authorization: User can only view their own PDFs unless admin
    if (pdf.UploadedByUserId != session.User.Id &&
        !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    // Deserialize progress from JSON
    ProcessingProgress? progress = null;
    if (!string.IsNullOrEmpty(pdf.ProcessingProgressJson))
    {
        try
        {
            progress = System.Text.Json.JsonSerializer.Deserialize<ProcessingProgress>(pdf.ProcessingProgressJson);
        }
        catch (Exception ex)
        {
            // Log error but return null progress instead of failing
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Failed to deserialize progress for PDF {PdfId}", pdfId);
        }
    }

    return Results.Ok(progress);
})
.RequireAuthorization()
.WithName("GetPdfProcessingProgress");

// PDF-08: Cancel PDF processing
v1Api.MapDelete("/pdfs/{pdfId}/processing", async (string pdfId, HttpContext context, MeepleAiDbContext db, IBackgroundTaskService backgroundTaskService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var pdf = await db.PdfDocuments
        .Where(p => p.Id == pdfId)
        .Select(p => new { p.Id, p.UploadedByUserId, p.ProcessingStatus })
        .FirstOrDefaultAsync(ct);

    if (pdf == null)
    {
        return Results.NotFound(new { error = "PDF not found" });
    }

    // Authorization: User can only cancel their own PDFs unless admin
    if (pdf.UploadedByUserId != session.User.Id &&
        !string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    // Check if processing is active
    if (pdf.ProcessingStatus == "completed" || pdf.ProcessingStatus == "failed")
    {
        return Results.BadRequest(new { error = "Processing already completed or failed" });
    }

    // Cancel the background task
    var cancelled = backgroundTaskService.CancelTask(pdfId);

    if (!cancelled)
    {
        logger.LogWarning("Failed to cancel processing for PDF {PdfId} - task not found", pdfId);
        return Results.BadRequest(new { error = "Processing task not found or already completed" });
    }

    logger.LogInformation("User {UserId} cancelled processing for PDF {PdfId}", session.User.Id, pdfId);

    return Results.Ok(new { message = "Processing cancellation requested" });
})
.RequireAuthorization()
.WithName("CancelPdfProcessing");

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

// EDIT-05: Enhanced Comments System endpoints
// 1. Create top-level comment
v1Api.MapPost("/rulespecs/{gameId}/{version}/comments", async (
    string gameId,
    string version,
    CreateCommentRequest request,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} creating comment on RuleSpec {GameId} version {Version}", userId, gameId, version);
        var comment = await commentService.CreateCommentAsync(
            gameId,
            version,
            request.LineNumber,
            request.CommentText,
            userId);
        logger.LogInformation("Comment {CommentId} created successfully", comment.Id);
        return Results.Created($"/api/v1/comments/{comment.Id}", comment);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create comment on RuleSpec {GameId} version {Version}", gameId, version);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization()
.WithName("CreateRuleSpecComment")
.WithTags("Comments")
.WithDescription("Create a top-level comment on a rule specification version")
.Produces<RuleCommentDto>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized);

// 2. Create reply to comment
v1Api.MapPost("/comments/{commentId}/replies", async (
    Guid commentId,
    CreateReplyRequest request,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} replying to comment {CommentId}", userId, commentId);
        var reply = await commentService.ReplyToCommentAsync(
            commentId,
            request.CommentText,
            userId);
        logger.LogInformation("Reply {ReplyId} created successfully", reply.Id);
        return Results.Created($"/api/v1/comments/{reply.Id}", reply);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("Comment {CommentId} not found for reply", commentId);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create reply to comment {CommentId}: {Error}", commentId, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error creating reply to comment {CommentId}", commentId);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization()
.WithName("CreateCommentReply")
.WithTags("Comments")
.WithDescription("Create a threaded reply to an existing comment")
.Produces<RuleCommentDto>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status404NotFound);

// 3. Get all comments for RuleSpec
v1Api.MapGet("/rulespecs/{gameId}/{version}/comments", async (
    string gameId,
    string version,
    bool includeResolved,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} (includeResolved: {IncludeResolved})",
        userId, gameId, version, includeResolved);

    var comments = await commentService.GetCommentsForRuleSpecAsync(gameId, version, includeResolved);
    return Results.Ok(comments);
})
.RequireAuthorization()
.WithName("GetRuleSpecComments")
.WithTags("Comments")
.WithDescription("Get all comments for a specific rule specification version (hierarchical structure)")
.Produces<IReadOnlyList<RuleCommentDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

// 4. Get comments for specific line
v1Api.MapGet("/rulespecs/{gameId}/{version}/lines/{lineNumber}/comments", async (
    string gameId,
    string version,
    int lineNumber,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    logger.LogInformation("User {UserId} fetching comments for RuleSpec {GameId} version {Version} line {LineNumber}",
        userId, gameId, version, lineNumber);

    var comments = await commentService.GetCommentsForLineAsync(gameId, version, lineNumber);
    return Results.Ok(comments);
})
.RequireAuthorization()
.WithName("GetLineComments")
.WithTags("Comments")
.WithDescription("Get all comments for a specific line in a rule specification")
.Produces<IReadOnlyList<RuleCommentDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

// 5. Resolve comment
v1Api.MapPost("/comments/{commentId}/resolve", async (
    Guid commentId,
    bool resolveReplies,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // SECURITY: Only Admin and Editor roles can resolve comments
    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to resolve comment without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} resolving comment {CommentId} (resolveReplies: {ResolveReplies})",
            userId, commentId, resolveReplies);

        var comment = await commentService.ResolveCommentAsync(commentId, userId, resolveReplies);
        logger.LogInformation("Comment {CommentId} resolved successfully", commentId);
        return Results.Ok(comment);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("Comment {CommentId} not found for resolution", commentId);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to resolve comment {CommentId}", commentId);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization()
.WithName("ResolveComment")
.WithTags("Comments")
.WithDescription("Mark a comment as resolved, optionally resolving all child replies (Admin/Editor only)")
.Produces<RuleCommentDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// 6. Unresolve comment
v1Api.MapPost("/comments/{commentId}/unresolve", async (
    Guid commentId,
    bool unresolveParent,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // SECURITY: Only Admin and Editor roles can unresolve comments
    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(session.User.Role, UserRole.Editor.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("User {UserId} with role {Role} attempted to unresolve comment without permission",
            session.User.Id, session.User.Role);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var userId = session.User.Id;

    // Manually resolve service from DI container
    var commentService = context.RequestServices.GetRequiredService<IRuleCommentService>();

    try
    {
        logger.LogInformation("User {UserId} unresolving comment {CommentId} (unresolveParent: {UnresolveParent})",
            userId, commentId, unresolveParent);

        var comment = await commentService.UnresolveCommentAsync(commentId, unresolveParent);
        logger.LogInformation("Comment {CommentId} unresolved successfully", commentId);
        return Results.Ok(comment);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("Comment {CommentId} not found for unresolve", commentId);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to unresolve comment {CommentId}", commentId);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireAuthorization()
.WithName("UnresolveComment")
.WithTags("Comments")
.WithDescription("Reopen a resolved comment, optionally unresolving parent if this is a reply (Admin/Editor only)")
.Produces<RuleCommentDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound);

// 7. User search for mentions (autocomplete)
v1Api.MapGet("/users/search", async (
    string query,
    MeepleAiDbContext db,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }
    var userId = session.User.Id;

    if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
    {
        return Results.Ok(Array.Empty<UserSearchResultDto>());
    }

    logger.LogInformation("User {UserId} searching for users with query: {Query}", userId, query);

    var users = await db.Users
        .Where(u => u.DisplayName.Contains(query) || u.Email.Contains(query))
        .OrderBy(u => u.DisplayName)
        .Take(10)
        .AsNoTracking()
        .Select(u => new UserSearchResultDto(u.Id, u.DisplayName, u.Email))
        .ToListAsync(ct);

    logger.LogInformation("Found {Count} users matching query: {Query}", users.Count, query);
    return Results.Ok(users);
})
.RequireAuthorization()
.WithName("SearchUsers")
.WithTags("Users")
.WithDescription("Search users by display name or email for @mention autocomplete (max 10 results)")
.Produces<IEnumerable<UserSearchResultDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

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

// AI-11: Quality tracking endpoints
v1Api.MapGet("/admin/quality/low-responses", async (
    HttpContext context,
    MeepleAiDbContext dbContext,
    int limit = 100,
    int offset = 0,
    DateTime? startDate = null,
    DateTime? endDate = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Convert DateTime parameters to UTC if they have Kind=Unspecified (from query string parsing)
    // PostgreSQL requires UTC DateTimes
    if (startDate.HasValue && startDate.Value.Kind == DateTimeKind.Unspecified)
        startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
    if (endDate.HasValue && endDate.Value.Kind == DateTimeKind.Unspecified)
        endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);

    // Query low-quality responses with filters
    var query = dbContext.AiRequestLogs.Where(log => log.IsLowQuality);

    if (startDate.HasValue)
        query = query.Where(log => log.CreatedAt >= startDate.Value);
    if (endDate.HasValue)
        query = query.Where(log => log.CreatedAt <= endDate.Value);

    var totalCount = await query.CountAsync(ct);
    var responses = await query
        .OrderByDescending(log => log.CreatedAt)
        .Skip(offset)
        .Take(limit)
        .Select(log => new LowQualityResponseDto(
            Guid.Parse(log.Id),
            log.CreatedAt,
            log.Query ?? string.Empty,
            log.RagConfidence ?? 0.0,
            log.LlmConfidence ?? 0.0,
            log.CitationQuality ?? 0.0,
            log.OverallConfidence ?? 0.0,
            log.IsLowQuality
        ))
        .ToListAsync(ct);

    return Results.Ok(new LowQualityResponsesResult(totalCount, responses));
})
.WithTags("Admin", "Quality")
.Produces<LowQualityResponsesResult>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

v1Api.MapGet("/admin/quality/report", async (
    HttpContext context,
    IQualityReportService reportService,
    int days = 7) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var endDate = DateTime.UtcNow;
    var startDate = endDate.AddDays(-days);
    var report = await reportService.GenerateReportAsync(startDate, endDate);
    return Results.Ok(report);
})
.WithTags("Admin", "Quality")
.Produces<QualityReport>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// ADM-02: n8n workflow configuration endpoints
v1Api.MapGet("/admin/n8n", async (HttpContext context, N8nConfigService n8nService, IFeatureFlagService featureFlags, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // CONFIG-05: Check if n8n integration feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.N8nIntegration"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "n8n integration is currently disabled", featureName = "Features.N8nIntegration" },
            statusCode: 403);
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

// ADMIN-02: Analytics dashboard endpoints
v1Api.MapGet("/admin/analytics", async (
    HttpContext context,
    IAdminStatsService statsService,
    DateTime? fromDate = null,
    DateTime? toDate = null,
    int days = 30,
    string? gameId = null,
    string? roleFilter = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var queryParams = new AnalyticsQueryParams(fromDate, toDate, days, gameId, roleFilter);
    var stats = await statsService.GetDashboardStatsAsync(queryParams, ct);
    return Results.Ok(stats);
})
.WithName("GetAnalytics")
.WithTags("Admin")
.WithDescription("Get analytics dashboard statistics with metrics and time-series trends")
.Produces<DashboardStatsDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

v1Api.MapPost("/admin/analytics/export", async (
    HttpContext context,
    IAdminStatsService statsService,
    ExportDataRequest request,
    CancellationToken ct = default) =>
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
        var data = await statsService.ExportDashboardDataAsync(request, ct);
        var contentType = request.Format.ToLowerInvariant() switch
        {
            "csv" => "text/csv",
            "json" => "application/json",
            _ => "text/plain"
        };
        var filename = $"analytics-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.{request.Format.ToLowerInvariant()}";

        return Results.File(
            System.Text.Encoding.UTF8.GetBytes(data),
            contentType,
            filename);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ExportAnalytics")
.WithTags("Admin")
.WithDescription("Export analytics dashboard data in CSV or JSON format")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

// ADMIN-01: User management endpoints
v1Api.MapGet("/admin/users", async (
    HttpContext context,
    UserManagementService userManagement,
    string? search = null,
    string? role = null,
    string? sortBy = null,
    string? sortOrder = "desc",
    int page = 1,
    int limit = 20,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await userManagement.GetUsersAsync(search, role, sortBy, sortOrder, page, limit, ct);
    return Results.Json(result);
})
.WithName("GetUsers")
.WithTags("Admin");

v1Api.MapPost("/admin/users", async (
    CreateUserRequest request,
    HttpContext context,
    UserManagementService userManagement,
    ILogger<Program> logger,
    CancellationToken ct) =>
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
        logger.LogInformation("Admin {AdminId} creating new user with email {Email}", session.User.Id, request.Email);
        var user = await userManagement.CreateUserAsync(request, ct);
        logger.LogInformation("User {UserId} created successfully", user.Id);
        return Results.Created($"/api/v1/admin/users/{user.Id}", user);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
    {
        logger.LogWarning("Failed to create user: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateUser")
.WithTags("Admin");

v1Api.MapPut("/admin/users/{id}", async (
    string id,
    UpdateUserRequest request,
    HttpContext context,
    UserManagementService userManagement,
    ILogger<Program> logger,
    CancellationToken ct) =>
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
        logger.LogInformation("Admin {AdminId} updating user {UserId}", session.User.Id, id);
        var user = await userManagement.UpdateUserAsync(id, request, ct);
        logger.LogInformation("User {UserId} updated successfully", id);
        return Results.Ok(user);
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("User {UserId} not found: {Error}", id, ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update user {UserId}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateUser")
.WithTags("Admin");

v1Api.MapDelete("/admin/users/{id}", async (
    string id,
    HttpContext context,
    UserManagementService userManagement,
    ILogger<Program> logger,
    CancellationToken ct) =>
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
        logger.LogInformation("Admin {AdminId} deleting user {UserId}", session.User.Id, id);
        await userManagement.DeleteUserAsync(id, session.User.Id, ct);
        logger.LogInformation("User {UserId} deleted successfully", id);
        return Results.NoContent();
    }
    catch (KeyNotFoundException ex)
    {
        logger.LogWarning("User {UserId} not found: {Error}", id, ex.Message);
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to delete user {UserId}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("DeleteUser")
.WithTags("Admin");

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

// CONFIG-01: Configuration management endpoints (Admin only)
v1Api.MapGet("/admin/configurations", async (
    HttpContext context,
    IConfigurationService configService,
    string? category = null,
    string? environment = null,
    bool activeOnly = true,
    int page = 1,
    int pageSize = 50,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await configService.GetConfigurationsAsync(category, environment, activeOnly, page, pageSize);
    return Results.Json(result);
})
.WithName("GetConfigurations")
.WithTags("Admin", "Configuration")
.Produces<PagedResult<SystemConfigurationDto>>();

v1Api.MapGet("/admin/configurations/{id}", async (
    string id,
    HttpContext context,
    IConfigurationService configService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await configService.GetConfigurationByIdAsync(id);
    return config != null ? Results.Json(config) : Results.NotFound();
})
.WithName("GetConfigurationById")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

v1Api.MapGet("/admin/configurations/key/{key}", async (
    string key,
    HttpContext context,
    IConfigurationService configService,
    string? environment = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var config = await configService.GetConfigurationByKeyAsync(key, environment);
    return config != null ? Results.Json(config) : Results.NotFound();
})
.WithName("GetConfigurationByKey")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

v1Api.MapPost("/admin/configurations", async (
    CreateConfigurationRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
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
        logger.LogInformation("Admin {AdminId} creating configuration {Key}", session.User.Id, request.Key);
        var config = await configService.CreateConfigurationAsync(request, session.User.Id);
        logger.LogInformation("Configuration {Key} created with ID {Id}", request.Key, config.Id);
        return Results.Created($"/api/v1/admin/configurations/{config.Id}", config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to create configuration {Key}: {Error}", request.Key, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CreateConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>(201)
.ProducesValidationProblem();

v1Api.MapPut("/admin/configurations/{id}", async (
    string id,
    UpdateConfigurationRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
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
        logger.LogInformation("Admin {AdminId} updating configuration {Id}", session.User.Id, id);
        var config = await configService.UpdateConfigurationAsync(id, request, session.User.Id);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for update", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} updated to version {Version}", id, config.Version);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Failed to update configuration {Id}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404)
.ProducesValidationProblem();

v1Api.MapDelete("/admin/configurations/{id}", async (
    string id,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} deleting configuration {Id}", session.User.Id, id);
    var success = await configService.DeleteConfigurationAsync(id);

    if (!success)
    {
        logger.LogWarning("Configuration {Id} not found for deletion", id);
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("Configuration {Id} deleted successfully", id);
    return Results.NoContent();
})
.WithName("DeleteConfiguration")
.WithTags("Admin", "Configuration")
.Produces(204)
.Produces(404);

v1Api.MapPatch("/admin/configurations/{id}/toggle", async (
    string id,
    bool isActive,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    logger.LogInformation("Admin {AdminId} toggling configuration {Id} to {Status}",
        session.User.Id, id, isActive ? "active" : "inactive");

    var config = await configService.ToggleConfigurationAsync(id, isActive, session.User.Id);

    if (config == null)
    {
        logger.LogWarning("Configuration {Id} not found for toggle", id);
        return Results.NotFound(new { error = "Configuration not found" });
    }

    logger.LogInformation("Configuration {Id} toggled to {Status}", id, config.IsActive ? "active" : "inactive");
    return Results.Json(config);
})
.WithName("ToggleConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

v1Api.MapPost("/admin/configurations/bulk-update", async (
    BulkConfigurationUpdateRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
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
        logger.LogInformation("Admin {AdminId} performing bulk update on {Count} configurations",
            session.User.Id, request.Updates.Count);

        var configs = await configService.BulkUpdateConfigurationsAsync(request, session.User.Id);

        logger.LogInformation("Bulk update completed successfully for {Count} configurations", configs.Count);
        return Results.Json(configs);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Bulk update failed: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("BulkUpdateConfigurations")
.WithTags("Admin", "Configuration")
.Produces<IReadOnlyList<SystemConfigurationDto>>()
.ProducesValidationProblem();

v1Api.MapPost("/admin/configurations/validate", async (
    string key,
    string value,
    string valueType,
    HttpContext context,
    IConfigurationService configService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value1) || value1 is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await configService.ValidateConfigurationAsync(key, value, valueType);
    return Results.Json(result);
})
.WithName("ValidateConfiguration")
.WithTags("Admin", "Configuration")
.Produces<ConfigurationValidationResult>();

v1Api.MapGet("/admin/configurations/export", async (
    string environment,
    HttpContext context,
    IConfigurationService configService,
    bool activeOnly = true,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var export = await configService.ExportConfigurationsAsync(environment, activeOnly);
    return Results.Json(export);
})
.WithName("ExportConfigurations")
.WithTags("Admin", "Configuration")
.Produces<ConfigurationExportDto>();

v1Api.MapPost("/admin/configurations/import", async (
    ConfigurationImportRequest request,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
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
        logger.LogInformation("Admin {AdminId} importing {Count} configurations",
            session.User.Id, request.Configurations.Count);

        var importedCount = await configService.ImportConfigurationsAsync(request, session.User.Id);

        logger.LogInformation("Successfully imported {Count} configurations", importedCount);
        return Results.Json(new { importedCount });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to import configurations");
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ImportConfigurations")
.WithTags("Admin", "Configuration")
.Produces<object>()
.ProducesValidationProblem();

// CONFIG-05: Feature flags management endpoints (Admin only)
v1Api.MapGet("/admin/features", async (
    HttpContext context,
    IFeatureFlagService featureFlags,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var flags = await featureFlags.GetAllFeatureFlagsAsync();
    return Results.Json(new { features = flags });
})
.WithName("GetAllFeatureFlags")
.WithTags("Admin", "FeatureFlags")
.WithDescription("List all feature flags with their current states (Admin only)")
.Produces<object>(200)
.Produces(401)
.Produces(403);

v1Api.MapPut("/admin/features/{featureName}", async (
    string featureName,
    FeatureFlagUpdateRequest request,
    HttpContext context,
    IFeatureFlagService featureFlags,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
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
        // Parse role from request if provided
        UserRole? role = null;
        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, out var parsedRole))
        {
            role = parsedRole;
        }

        if (request.Enabled)
        {
            await featureFlags.EnableFeatureAsync(featureName, role, session.User.Id);
            logger.LogInformation("Admin {UserId} enabled feature {FeatureName}{RoleInfo}",
                session.User.Id, featureName, role.HasValue ? $" for {role.Value}" : "");
        }
        else
        {
            await featureFlags.DisableFeatureAsync(featureName, role, session.User.Id);
            logger.LogInformation("Admin {UserId} disabled feature {FeatureName}{RoleInfo}",
                session.User.Id, featureName, role.HasValue ? $" for {role.Value}" : "");
        }

        return Results.Json(new { featureName, enabled = request.Enabled, role = request.Role });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to update feature flag {FeatureName}", featureName);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("UpdateFeatureFlag")
.WithTags("Admin", "FeatureFlags")
.WithDescription("Enable or disable a feature flag globally or for a specific role (Admin only)")
.Produces<object>(200)
.Produces(400)
.Produces(401)
.Produces(403);

v1Api.MapGet("/admin/configurations/{id}/history", async (
    string id,
    HttpContext context,
    IConfigurationService configService,
    int limit = 20,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var history = await configService.GetConfigurationHistoryAsync(id, limit);
    return Results.Json(history);
})
.WithName("GetConfigurationHistory")
.WithTags("Admin", "Configuration")
.Produces<IReadOnlyList<ConfigurationHistoryDto>>();

v1Api.MapPost("/admin/configurations/{id}/rollback/{version:int}", async (
    string id,
    int version,
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    CancellationToken ct = default) =>
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
        logger.LogInformation("Admin {AdminId} rolling back configuration {Id} to version {Version}",
            session.User.Id, id, version);

        var config = await configService.RollbackConfigurationAsync(id, version, session.User.Id);

        if (config == null)
        {
            logger.LogWarning("Configuration {Id} not found for rollback", id);
            return Results.NotFound(new { error = "Configuration not found" });
        }

        logger.LogInformation("Configuration {Id} rolled back successfully", id);
        return Results.Json(config);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning("Rollback failed for configuration {Id}: {Error}", id, ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("RollbackConfiguration")
.WithTags("Admin", "Configuration")
.Produces<SystemConfigurationDto>()
.Produces(404);

v1Api.MapGet("/admin/configurations/categories", async (
    HttpContext context,
    IConfigurationService configService,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var categories = await configService.GetCategoriesAsync();
    return Results.Json(categories);
})
.WithName("GetCategories")
.WithTags("Admin", "Configuration")
.Produces<IReadOnlyList<string>>();

v1Api.MapPost("/admin/configurations/cache/invalidate", async (
    HttpContext context,
    IConfigurationService configService,
    ILogger<Program> logger,
    string? key = null,
    CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (key != null)
    {
        logger.LogInformation("Admin {AdminId} invalidating cache for configuration key {Key}", session.User.Id, key);
        await configService.InvalidateCacheAsync(key);
    }
    else
    {
        logger.LogInformation("Admin {AdminId} invalidating all configuration cache", session.User.Id);
        await configService.InvalidateCacheAsync();
    }

    return Results.Json(new { ok = true, message = key != null ? $"Cache invalidated for key: {key}" : "All configuration cache invalidated" });
})
.WithName("InvalidateConfigurationCache")
.WithTags("Admin", "Configuration")
.Produces<object>();

v1Api.MapGet("/users/me/sessions", async (HttpContext context, ISessionManagementService sessionManagement, CancellationToken ct = default) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    var sessions = await sessionManagement.GetUserSessionsAsync(session.User.Id, ct);
    return Results.Json(sessions);
});

// PERF-03: Cache management endpoints
v1Api.MapGet("/admin/cache/stats", async (HttpContext context, IAiResponseCacheService cacheService, string? gameId = null, CancellationToken ct = default) =>
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
        var stats = await cacheService.GetCacheStatsAsync(gameId, ct);
        return Results.Json(stats);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: $"Failed to retrieve cache stats: {ex.Message}", statusCode: 500);
    }
})
.WithName("GetCacheStats")
.WithDescription("Get cache statistics with optional game filter (Admin only)")
.WithTags("Admin", "Cache")
.Produces<CacheStats>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError);

v1Api.MapDelete("/admin/cache/games/{gameId}", async (string gameId, HttpContext context, IAiResponseCacheService cacheService, MeepleAiDbContext dbContext, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(gameId))
    {
        return Results.BadRequest(new { error = "gameId is required" });
    }

    // Validate game exists
    var gameExists = await dbContext.Games.AnyAsync(g => g.Id == gameId, ct);
    if (!gameExists)
    {
        logger.LogWarning("Admin {AdminId} attempted to invalidate cache for non-existent game {GameId}", session.User.Id, gameId);
        return Results.NotFound(new { error = $"Game with ID '{gameId}' not found" });
    }

    try
    {
        logger.LogInformation("Admin {AdminId} invalidating cache for game {GameId}", session.User.Id, gameId);
        await cacheService.InvalidateGameAsync(gameId, ct);
        logger.LogInformation("Successfully invalidated cache for game {GameId}", gameId);
        return Results.Json(new { ok = true, message = $"Cache invalidated for game '{gameId}'" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to invalidate cache for game {GameId}", gameId);
        return Results.Problem(detail: $"Failed to invalidate cache: {ex.Message}", statusCode: 500);
    }
})
.WithName("InvalidateGameCache")
.WithDescription("Invalidate all cached responses for a specific game (Admin only)")
.WithTags("Admin", "Cache")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status500InternalServerError);

v1Api.MapDelete("/admin/cache/tags/{tag}", async (string tag, HttpContext context, IAiResponseCacheService cacheService, ILogger<Program> logger, CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(tag))
    {
        return Results.BadRequest(new { error = "tag is required" });
    }

    try
    {
        logger.LogInformation("Admin {AdminId} invalidating cache by tag {Tag}", session.User.Id, tag);
        await cacheService.InvalidateByCacheTagAsync(tag, ct);
        logger.LogInformation("Successfully invalidated cache by tag {Tag}", tag);
        return Results.Json(new { ok = true, message = $"Cache invalidated for tag '{tag}'" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to invalidate cache by tag {Tag}", tag);
        return Results.Problem(detail: $"Failed to invalidate cache: {ex.Message}", statusCode: 500);
    }
})
.WithName("InvalidateCacheByTag")
.WithDescription("Invalidate cache entries by tag (e.g., game:chess, pdf:abc123) (Admin only)")
.WithTags("Admin", "Cache")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError);

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

// CHAT-06: Message editing endpoint
v1Api.MapPut("/chats/{chatId:guid}/messages/{messageId:guid}",
    async (
        Guid chatId,
        Guid messageId,
        UpdateMessageRequest request,
        HttpContext context,
        ChatService chatService,
        IFeatureFlagService featureFlags,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
        {
            return Results.Unauthorized();
        }

        // CONFIG-05: Check if message edit/delete feature is enabled
        if (!await featureFlags.IsEnabledAsync("Features.MessageEditDelete"))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "Message modification is currently disabled", featureName = "Features.MessageEditDelete" },
                statusCode: 403);
        }

        try
        {
            logger.LogInformation("User {UserId} updating message {MessageId} in chat {ChatId}", session.User.Id, messageId, chatId);
            var updatedMessage = await chatService.UpdateMessageAsync(chatId, messageId, request.Content, session.User.Id, ct);

            var response = MapToChatMessageResponse(updatedMessage);
            logger.LogInformation("Message {MessageId} updated successfully by user {UserId}", messageId, session.User.Id);
            return Results.Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Message {MessageId} not found in chat {ChatId}: {Error}", messageId, chatId, ex.Message);
            return Results.NotFound(new { error = "message_not_found", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("User {UserId} not authorized to update message {MessageId}: {Error}", session.User.Id, messageId, ex.Message);
            return Results.Problem(statusCode: 403, detail: ex.Message, title: "Forbidden");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Invalid operation updating message {MessageId}: {Error}", messageId, ex.Message);
            return Results.BadRequest(new { error = "invalid_operation", message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating message {MessageId} in chat {ChatId}", messageId, chatId);
            return Results.Problem(statusCode: 500, detail: "An error occurred while updating the message", title: "Internal Server Error");
        }
    })
    .RequireAuthorization()
    .WithName("UpdateChatMessage")
    .WithDescription("Edit the content of an existing user message. Invalidates subsequent AI responses. Requires authentication. Users can only edit their own messages.")
    .WithTags("Chat");

// CHAT-06: Message deletion endpoint
v1Api.MapDelete("/chats/{chatId:guid}/messages/{messageId:guid}",
    async (
        Guid chatId,
        Guid messageId,
        HttpContext context,
        ChatService chatService,
        IFeatureFlagService featureFlags,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
        {
            return Results.Unauthorized();
        }

        // CONFIG-05: Check if message edit/delete feature is enabled
        if (!await featureFlags.IsEnabledAsync("Features.MessageEditDelete"))
        {
            return Results.Json(
                new { error = "feature_disabled", message = "Message modification is currently disabled", featureName = "Features.MessageEditDelete" },
                statusCode: 403);
        }

        var isAdmin = string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

        try
        {
            logger.LogInformation("User {UserId} deleting message {MessageId} in chat {ChatId} (admin: {IsAdmin})", session.User.Id, messageId, chatId, isAdmin);
            var deleted = await chatService.DeleteMessageAsync(chatId, messageId, session.User.Id, isAdmin, ct);

            if (!deleted)
            {
                logger.LogInformation("Message {MessageId} already deleted", messageId);
                return Results.Ok(new { message = "Message already deleted" });
            }

            logger.LogInformation("Message {MessageId} deleted successfully by user {UserId}", messageId, session.User.Id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Message {MessageId} not found in chat {ChatId}: {Error}", messageId, chatId, ex.Message);
            return Results.NotFound(new { error = "message_not_found", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("User {UserId} not authorized to delete message {MessageId}: {Error}", session.User.Id, messageId, ex.Message);
            return Results.Problem(statusCode: 403, detail: ex.Message, title: "Forbidden");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting message {MessageId} in chat {ChatId}", messageId, chatId);
            return Results.Problem(statusCode: 500, detail: "An error occurred while deleting the message", title: "Internal Server Error");
        }
    })
    .RequireAuthorization()
    .WithName("DeleteChatMessage")
    .WithDescription("Soft-delete a message. Users can delete their own messages; admins can delete any message. Invalidates subsequent AI responses. Requires authentication.")
    .WithTags("Chat");

// CHAT-05: Export chat endpoint
v1Api.MapPost("/chats/{chatId:guid}/export", async (
    Guid chatId,
    ExportChatRequest request,
    IChatExportService exportService,
    IFeatureFlagService featureFlags,
    HttpContext context,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    // CONFIG-05: Check if chat export feature is enabled
    if (!await featureFlags.IsEnabledAsync("Features.ChatExport"))
    {
        return Results.Json(
            new { error = "feature_disabled", message = "Chat export is currently unavailable", featureName = "Features.ChatExport" },
            statusCode: 403);
    }

    try
    {
        var result = await exportService.ExportChatAsync(
            chatId,
            session.User.Id,
            request.Format,
            request.DateFrom,
            request.DateTo,
            ct);

        if (!result.Success)
        {
            return result.Error switch
            {
                "not_found" => Results.NotFound(new { error = result.ErrorDetails }),
                "unsupported_format" => Results.BadRequest(new { error = result.ErrorDetails }),
                "generation_failed" => Results.Problem(
                    detail: result.ErrorDetails,
                    statusCode: StatusCodes.Status500InternalServerError),
                _ => Results.Problem("Unknown error occurred during export")
            };
        }

        logger.LogInformation("User {UserId} exported chat {ChatId} in {Format} format",
            session.User.Id, chatId, request.Format);

        // Return stream with proper content disposition for download
        return Results.Stream(
            result.Stream!,
            contentType: result.ContentType,
            fileDownloadName: result.Filename,
            enableRangeProcessing: false);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error during chat export for user {UserId}, chat {ChatId}",
            session.User.Id, chatId);
        return Results.Problem("An unexpected error occurred during export");
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

// CHAT-06: Helper method to map ChatLogEntity to ChatMessageResponse
static ChatMessageResponse MapToChatMessageResponse(ChatLogEntity entity)
{
    return new ChatMessageResponse(
        entity.Id,
        entity.ChatId,
        entity.UserId,
        entity.Level,
        entity.Message,
        entity.SequenceNumber,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.IsDeleted,
        entity.DeletedAt,
        entity.DeletedByUserId,
        entity.IsInvalidated,
        entity.MetadataJson
    );
}

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