# SOLID Refactoring Plan - MeepleAI Monorepo

## Executive Summary

This document outlines a comprehensive SOLID refactoring plan for the MeepleAI codebase to address critical architectural issues, particularly class size and Single Responsibility Principle violations.

**Current State:**
- Program.cs: 6,972 lines (CRITICAL)
- RagService.cs: 1,298 lines
- PdfTableExtractionService.cs: 1,041 lines
- QdrantService.cs: 1,027 lines
- PdfStorageService.cs: 1,026 lines
- MeepleAiDbContext.cs: 745 lines

**Target State:**
- Program.cs: ~150 lines (modularized with extensions)
- Service classes: 200-400 lines average (extracted responsibilities)
- DbContext: ~100 lines (extracted entity configurations)

## PHASE 1: Program.cs Modularization

### Priority: CRITICAL
### Estimated Time: 3-4 hours
### Risk: LOW (just moving code, no logic changes)

### 1.1 Create Extension Infrastructure

**New Folders:**
```
apps/api/src/Api/
├── Extensions/
│   ├── InfrastructureServiceExtensions.cs
│   ├── ApplicationServiceExtensions.cs
│   ├── ObservabilityServiceExtensions.cs
│   ├── AuthenticationServiceExtensions.cs
│   └── WebApplicationExtensions.cs
└── Routing/
    ├── AuthEndpoints.cs
    ├── GameEndpoints.cs
    ├── AdminEndpoints.cs
    ├── AiEndpoints.cs
    └── PdfEndpoints.cs
```

### 1.2 InfrastructureServiceExtensions.cs

**Responsibility:** Database, caching, HTTP clients, external integrations

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Polly;
using Api.Infrastructure;

namespace Api.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddDatabaseServices(configuration, environment);
        services.AddCachingServices(configuration);
        services.AddHttpClients(configuration);

        return services;
    }

    private static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Only configure Postgres in non-test environments
        if (!environment.IsEnvironment("Testing"))
        {
            var connectionString = configuration.GetConnectionString("Postgres")
                ?? configuration["ConnectionStrings__Postgres"]
                ?? throw new InvalidOperationException("Missing Postgres connection string");

            // PERF-09: Optimize Postgres connection pooling
            services.AddDbContext<MeepleAiDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                    npgsqlOptions.MaxBatchSize(100);
                });

                options.EnableSensitiveDataLogging(environment.IsDevelopment());
                options.EnableDetailedErrors(environment.IsDevelopment());
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });
        }

        return services;
    }

    private static IServiceCollection AddCachingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PERF-09: Configure Redis with optimized connection pooling
        var redisUrl = configuration["REDIS_URL"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(redisUrl);
            config.AbortOnConnectFail = false;
            config.ConnectRetry = 3;
            config.ConnectTimeout = 5000;
            config.SyncTimeout = 5000;
            config.KeepAlive = 60;
            config.AllowAdmin = false;
            config.DefaultDatabase = 0;

            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Connecting to Redis at {RedisUrl}", redisUrl);

            return ConnectionMultiplexer.Connect(config);
        });

        // PERF-05: Configure HybridCache with L1 + L2 support
        var hybridCacheConfig = configuration.GetSection("HybridCache")
            .Get<HybridCacheConfiguration>() ?? new HybridCacheConfiguration();

        if (hybridCacheConfig.EnableL2Redis)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisUrl;
                options.InstanceName = "MeepleAi:";
            });
        }

        services.AddHybridCache();
        services.AddScoped<IHybridCacheService, HybridCacheService>();

        return services;
    }

    private static IServiceCollection AddHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PERF-09: Configure HttpClient with connection pooling
        services.AddHttpClient("Ollama", client =>
        {
            var ollamaUrl = configuration["OLLAMA_URL"] ?? "http://ollama:11434";
            client.BaseAddress = new Uri(ollamaUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            MaxConnectionsPerServer = 10
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        // OpenAI client
        services.AddHttpClient("OpenAI", client =>
        {
            var apiKey = configuration["OPENAI_API_KEY"];
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
            client.Timeout = TimeSpan.FromMinutes(3);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 20
        });

        // OpenRouter client
        services.AddHttpClient("OpenRouter", client =>
        {
            var apiKey = configuration["OPENROUTER_API_KEY"];
            client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
            client.Timeout = TimeSpan.FromMinutes(3);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 20
        });

        // Qdrant client
        services.AddHttpClient("Qdrant", client =>
        {
            var qdrantUrl = configuration["QDRANT_URL"] ?? "http://qdrant:6333";
            client.BaseAddress = new Uri(qdrantUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 10
        });

        // AI-13: BoardGameGeek API client with retry logic
        services.AddHttpClient("BoardGameGeek", client =>
        {
            client.BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/");
            client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI-Bot/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 5
        });

        return services;
    }
}
```

### 1.3 ApplicationServiceExtensions.cs

**Responsibility:** Application-level services (domain, AI, admin)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Api.Services;
using Api.Services.Chat;

namespace Api.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddDomainServices();
        services.AddAiServices();
        services.AddAdminServices();

        return services;
    }

    private static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Game and RuleSpec services
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IRuleSpecService, RuleSpecService>();
        services.AddScoped<IRuleSpecDiffService, RuleSpecDiffService>();
        services.AddScoped<IRuleSpecCommentService, RuleSpecCommentService>();

        // PDF services
        services.AddScoped<IPdfStorageService, PdfStorageService>();
        services.AddScoped<IPdfTextExtractionService, PdfTextExtractionService>();
        services.AddScoped<IPdfTableExtractionService, PdfTableExtractionService>();
        services.AddScoped<IPdfValidationService, PdfValidationService>();

        // Chat services
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatExportService, ChatExportService>();
        services.AddScoped<IFollowUpQuestionService, FollowUpQuestionService>();

        return services;
    }

    private static IServiceCollection AddAiServices(this IServiceCollection services)
    {
        // Vector search services
        services.AddSingleton<IQdrantClientAdapter, QdrantClientAdapter>();
        services.AddScoped<IQdrantService, QdrantService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();

        // RAG and LLM services
        services.AddScoped<IRagService, RagService>();
        services.AddScoped<ILlmService, LlmService>();
        services.AddScoped<IStreamingRagService, StreamingRagService>();
        services.AddScoped<IStreamingQaService, StreamingQaService>();
        services.AddScoped<IStreamingSetupService, StreamingSetupService>();

        // AI support services
        services.AddScoped<ILanguageDetectionService, LanguageDetectionService>();
        services.AddScoped<IAiResponseCacheService, AiResponseCacheService>();
        services.AddScoped<IResponseQualityService, ResponseQualityService>();
        services.AddScoped<IRagEvaluationService, RagEvaluationService>();

        // AI-14: Hybrid search services
        services.AddScoped<IKeywordSearchService, KeywordSearchService>();
        services.AddScoped<IHybridSearchService, HybridSearchService>();

        // AI-13: BoardGameGeek integration
        services.AddScoped<IBggApiService, BggApiService>();

        // CHESS-03/04: Chess services
        services.AddScoped<IChessKnowledgeService, ChessKnowledgeService>();
        services.AddScoped<IChessAgentService, ChessAgentService>();

        // AI-07: Prompt management
        services.AddScoped<IPromptManagementService, PromptManagementService>();
        services.AddScoped<IPromptEvaluationService, PromptEvaluationService>();

        // AI-03: Setup guide service
        services.AddScoped<ISetupGuideService, SetupGuideService>();

        return services;
    }

    private static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        // ADMIN-01: User management
        services.AddScoped<IUserManagementService, UserManagementService>();

        // ADMIN-02: Analytics
        services.AddScoped<IAdminStatsService, AdminStatsService>();

        // CONFIG-01: Dynamic configuration
        services.AddScoped<IConfigurationService, ConfigurationService>();

        // CONFIG-05: Feature flags
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        // N8N-05: Workflow error logging
        services.AddScoped<IWorkflowErrorLoggingService, WorkflowErrorLoggingService>();

        // OPS-07: Alerting system
        services.AddScoped<IAlertingService, AlertingService>();

        return services;
    }
}
```

### 1.4 AuthenticationServiceExtensions.cs

**Responsibility:** Authentication, authorization, session management

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Api.Services;

namespace Api.Extensions;

public static class AuthenticationServiceExtensions
{
    public static IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AUTH-06: Data Protection API for OAuth token encryption
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo("./keys"));

        // Authentication services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApiKeyAuthenticationService, ApiKeyAuthenticationService>();
        services.AddScoped<IApiKeyManagementService, ApiKeyManagementService>();

        // AUTH-03: Session management
        services.AddScoped<ISessionManagementService, SessionManagementService>();
        services.AddHostedService<SessionAutoRevocationService>();
        services.AddScoped<ISessionCacheService, SessionCacheService>();

        // AUTH-06: OAuth services
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IEncryptionService, EncryptionService>();

        // AUTH-07: Two-factor authentication
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<ITempSessionService, TempSessionService>();

        // AUTH-04: Password reset
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IEmailService, EmailService>();

        // Rate limiting
        services.AddScoped<IRateLimitService, RateLimitService>();

        return services;
    }
}
```

### 1.5 ObservabilityServiceExtensions.cs

**Responsibility:** Logging, metrics, tracing, health checks

```csharp
using Microsoft.Extensions.DependencyInjection;
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
        services.AddHealthCheckServices(configuration);

        return services;
    }

    private static IServiceCollection AddOpenTelemetryServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var serviceName = "MeepleAI.Api";
        var serviceVersion = "1.0.0";

        // OPS-02: Configure OpenTelemetry tracing
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment.EnvironmentName,
                    ["service.namespace"] = "MeepleAI"
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                    {
                        return !httpContext.Request.Path.StartsWithSegments("/health");
                    };
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(MeepleAiActivitySources.Main.Name)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                        ?? "http://jaeger:4318");
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MeepleAiMetrics.MeterName)
                .AddPrometheusExporter());

        return services;
    }

    private static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks();

        // OPS-01: Database health check
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? configuration["ConnectionStrings__Postgres"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecks.AddNpgSql(connectionString, name: "postgres");
        }

        // Redis health check
        healthChecks.AddRedis(
            configuration["REDIS_URL"] ?? "localhost:6379",
            name: "redis");

        // Qdrant health check
        healthChecks.AddCheck<QdrantHealthCheck>("qdrant");

        return services;
    }
}
```

### 1.6 WebApplicationExtensions.cs

**Responsibility:** Middleware pipeline and endpoint configuration

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Api.Middleware;

namespace Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigureMiddlewarePipeline(this WebApplication app)
    {
        // Response compression (must be before other middleware)
        app.UseResponseCompression();

        // Forwarded headers (must be early in pipeline)
        app.UseForwardedHeaders();

        // CORS
        app.UseCors("AllowAll");

        // Exception handling
        app.UseMiddleware<ApiExceptionHandlerMiddleware>();

        // Authentication & Authorization
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.UseAuthorization();

        // API key quota enforcement
        app.UseMiddleware<ApiKeyQuotaEnforcementMiddleware>();

        return app;
    }

    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        // Health checks (unversioned infrastructure endpoints)
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready");
        app.MapHealthChecks("/health/live");

        // Prometheus metrics
        app.MapPrometheusScrapingEndpoint("/metrics");

        // Swagger (development only)
        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/api/docs", () => Results.Redirect("/swagger"))
                .ExcludeFromDescription();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MeepleAI API v1");
                c.RoutePrefix = "swagger";
            });
        }

        // API v1 endpoints (configured in routing classes)
        var v1Api = app.MapGroup("/api/v1");
        v1Api.ConfigureAuthEndpoints();
        v1Api.ConfigureGameEndpoints();
        v1Api.ConfigureAdminEndpoints();
        v1Api.ConfigureAiEndpoints();
        v1Api.ConfigurePdfEndpoints();

        return app;
    }
}
```

### 1.7 Refactored Program.cs

**Target: ~150 lines**

```csharp
using Api.Extensions;
using Api.Logging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = LoggingConfiguration.ConfigureSerilog(builder).CreateLogger();
builder.Host.UseSerilog();

// Configure services
builder.Services.Configure<SessionCookieConfiguration>(
    builder.Configuration.GetSection("Authentication:SessionCookie"));
builder.Services.Configure<SessionManagementConfiguration>(
    builder.Configuration.GetSection("Authentication:SessionManagement"));
builder.Services.Configure<OAuthConfiguration>(
    builder.Configuration.GetSection("Authentication:OAuth"));
builder.Services.Configure<RateLimitConfiguration>(
    builder.Configuration.GetSection("RateLimit"));
builder.Services.Configure<PdfProcessingConfiguration>(
    builder.Configuration.GetSection("PdfProcessing"));
builder.Services.Configure<FollowUpQuestionsConfiguration>(
    builder.Configuration.GetSection("FollowUpQuestions"));
builder.Services.Configure<RagPromptsConfiguration>(
    builder.Configuration.GetSection("RagPrompts"));
builder.Services.Configure<HybridCacheConfiguration>(
    builder.Configuration.GetSection("HybridCache"));
builder.Services.Configure<HybridSearchConfiguration>(
    builder.Configuration.GetSection("HybridSearch"));

// Add services using extension methods
builder.Services.AddResponseCompression();  // Keep inline for brevity
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["FRONTEND_URL"] ?? "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices();
builder.Services.AddAuthenticationServices(builder.Configuration);
builder.Services.AddObservabilityServices(builder.Configuration, builder.Environment);

builder.Services.AddTimeProvider();
builder.Services.AddHostedService<BackgroundTaskService>();
builder.Services.AddAuditService();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Build application
var app = builder.Build();

// Run migrations if not in testing
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    dbContext.Database.Migrate();
}

// Configure pipeline and endpoints
app.ConfigureMiddlewarePipeline();
app.ConfigureEndpoints();

app.Run();
```

## Implementation Steps

1. **Create folder structure:**
   ```bash
   mkdir -p apps/api/src/Api/Extensions
   mkdir -p apps/api/src/Api/Routing
   ```

2. **Create extension files in order:**
   - InfrastructureServiceExtensions.cs
   - ApplicationServiceExtensions.cs
   - AuthenticationServiceExtensions.cs
   - ObservabilityServiceExtensions.cs
   - WebApplicationExtensions.cs

3. **Create routing files:**
   - AuthEndpoints.cs (extract auth, OAuth, 2FA endpoints)
   - GameEndpoints.cs (extract game, rulespec endpoints)
   - AdminEndpoints.cs (extract admin, config, prompts endpoints)
   - AiEndpoints.cs (extract RAG, chat, streaming endpoints)
   - PdfEndpoints.cs (extract PDF upload, processing endpoints)

4. **Update Program.cs:**
   - Replace massive sections with extension method calls
   - Keep only essential configuration
   - Target: reduce from 6972 to ~150 lines

5. **Test:**
   ```bash
   cd apps/api
   dotnet build
   dotnet test
   ```

6. **Commit:**
   ```bash
   git add .
   git commit -m "refactor: modularize Program.cs into extension methods (SOLID SRP)"
   ```

## Success Criteria

- [ ] Program.cs reduced to ~150 lines
- [ ] All extension files created and properly namespace
- [ ] All endpoint routing files created
- [ ] Build succeeds without errors
- [ ] All tests pass (no regressions)
- [ ] Code follows SOLID principles (SRP in particular)
- [ ] No duplicate code
- [ ] Proper dependency injection maintained

## Next Phases

After Phase 1 completion:
- **Phase 2:** Extract entity configurations from DbContext
- **Phase 3:** Refactor service layer (RagService, QdrantService, PDF services)

## Notes

- This refactoring is **SAFE** - we're only moving code, not changing logic
- Git commits after each file creation for easy rollback
- Extension pattern is standard ASP.NET Core best practice
- Makes Program.cs maintainable and testable
