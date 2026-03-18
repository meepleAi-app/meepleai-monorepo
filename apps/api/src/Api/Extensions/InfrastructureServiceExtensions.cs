using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Polly;
using Polly.Extensions.Http;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundTasks;
using Api.Services;
using Api.Configuration;
using Api.Models;
using Api.SharedKernel.Application.Services;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore; // ISSUE-3493: pgvector support

namespace Api.Extensions;

internal static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddDatabaseServices(configuration, environment);
        services.AddCachingServices(configuration);
        services.AddHttpClients(configuration);
        services.AddTimeProvider();
        services.AddBackgroundServices();

        // Prevent unhandled background service exceptions from crashing the host.
        // Safety net for schema mismatches (integration mode) or transient DB failures.
        services.Configure<HostOptions>(options =>
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        services.AddStorageServices(); // Issue #2732: Storage services

        return services;
    }

    private static IServiceCollection AddStorageServices(this IServiceCollection services)
    {
        // Issue #2732: File storage service for document operations
        services.AddScoped<Infrastructure.Services.IStorageService, Infrastructure.Services.LocalStorageService>();

        return services;
    }

    private static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Only configure Postgres in non-test environments (tests will override with SQLite)
        if (!environment.IsEnvironment("Testing"))
        {
            // SEC-03: Debug block removed (Issue #2152 resolved). Connection string must never be logged.

            // Issue #2152: Try SecretsHelper FIRST (reads POSTGRES_* vars)
            var secretsHelperResult = SecretsHelper.BuildPostgresConnectionString(configuration);

            // SEC-708: Build connection string from Docker Secrets if available
            var envVarConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
            var connectionString = secretsHelperResult != null
                ? secretsHelperResult
                : (envVarConnectionString
                    ?? configuration["ConnectionStrings__Postgres"]
                    ?? configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("No PostgreSQL connection string configured"));

            // PERF-09: Optimize Postgres connection pooling for better throughput
            services.AddDbContext<MeepleAiDbContext>(options =>
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

                    // ISSUE-3493: Enable pgvector extension for vector similarity search
                    npgsqlOptions.UseVector();
                });

                // Performance optimizations
                options.EnableSensitiveDataLogging(environment.IsDevelopment());
                options.EnableDetailedErrors(environment.IsDevelopment());

                // Query behavior
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // PERF-06: Default to no-tracking
            });

            // Register domain event collector as scoped (per-request lifecycle)
            // This ensures events are collected and dispatched within the same HTTP request
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        }

        return services;
    }

    private static IServiceCollection AddCachingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PERF-09: Configure Redis with optimized connection pooling
        // Issue #2152: Priority: Environment variable > Configuration > Default
        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST")
            ?? configuration["REDIS_HOST"]
            ?? "localhost";
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT")
            ?? configuration["REDIS_PORT"]
            ?? "6379";

        // Get Redis password from secrets or environment
        var redisPassword = SecretsHelper.GetSecretOrValue(configuration, "REDIS_PASSWORD", logger: null, required: false)
            ?? Environment.GetEnvironmentVariable("REDIS_PASSWORD");

        Console.WriteLine($"[DEBUG #2152] Redis config: Host={redisHost}, Port={redisPort}, Password={(!string.IsNullOrEmpty(redisPassword) ? "SET" : "NOT SET")}");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = new ConfigurationOptions
            {
                EndPoints = { $"{redisHost}:{redisPort}" }
            };

            // Add password if configured
            if (!string.IsNullOrWhiteSpace(redisPassword))
            {
                config.Password = redisPassword;
            }

            // Connection resilience
            config.AbortOnConnectFail = false; // Fail gracefully if Redis unavailable
            config.ConnectRetry = 3;
            config.ConnectTimeout = 5000; // 5 seconds
            config.SyncTimeout = 5000; // 5 seconds

            // Performance optimizations
            config.KeepAlive = 60; // Keep-alive every 60 seconds
            config.AllowAdmin = false; // Disable admin commands for security

            // Connection pooling (StackExchange.Redis manages pool internally)
            config.DefaultDatabase = 0;

            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Connecting to Redis at {RedisHost}:{RedisPort} with optimized settings", redisHost, redisPort);

            return ConnectionMultiplexer.Connect(config);
        });

        // PERF-05: Configure HybridCache with conditional L1 (in-memory) + L2 (Redis) support
        var hybridCacheConfig = configuration.GetSection("HybridCache").Get<HybridCacheConfiguration>()
            ?? new HybridCacheConfiguration();

#pragma warning disable EXTEXP0018 // HybridCache preview APIs
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = hybridCacheConfig.MaximumPayloadBytes;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = hybridCacheConfig.DefaultExpiration,
                LocalCacheExpiration = hybridCacheConfig.DefaultExpiration
            };
        });
#pragma warning restore EXTEXP0018

        // Add L2 distributed cache (Redis) if enabled, otherwise use in-memory fallback
        // IDistributedCache is required by: EditorLockService, ShareLink handlers, AddCommentToSharedThread
        if (hybridCacheConfig.EnableL2Cache)
        {
            // Build Redis connection string with password if configured
            var redisConnectionString = string.IsNullOrWhiteSpace(redisPassword)
                ? $"{redisHost}:{redisPort}"
                : $"{redisHost}:{redisPort},password={redisPassword}";

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "meepleai:hybridcache:";
            });
        }
        else
        {
            // Fallback to in-memory distributed cache when Redis L2 is disabled
            // Required for services that depend on IDistributedCache directly
            services.AddDistributedMemoryCache();
        }

        // Register HybridCache service wrapper
        // Singleton: HybridCacheService is thread-safe (uses Interlocked, thread-safe HybridCache and Redis)
        // Required for ILlmTierRoutingService and other singleton services that depend on caching
        services.AddSingleton<IHybridCacheService, HybridCacheService>();

        // AI-10: Cache optimization services
        services.Configure<CacheOptimizationConfiguration>(
            configuration.GetSection("CacheOptimization"));
        services.AddSingleton<ICacheMetricsRecorder, CacheMetricsRecorder>();
        services.AddSingleton<IDynamicTtlStrategy, DynamicTtlStrategy>();
        services.AddSingleton<IRedisFrequencyTracker, RedisFrequencyTracker>();

        // Issue #4275: BGG API tier-based rate limiting configuration
        services.Configure<Api.Middleware.BggRateLimitOptions>(
            configuration.GetSection("BggRateLimit"));

        // AI-10: Conditional cache warming service (disabled by default)
        var cacheWarmingEnabled = configuration
            .GetSection("CacheOptimization:WarmingEnabled")
            .Get<bool>();
        if (cacheWarmingEnabled)
        {
            services.AddHostedService<CacheWarmingService>();
        }

        // PERF-02: Session caching (Phase 2 optimization)
        services.AddSingleton<ISessionCacheService, SessionCacheService>();

        return services;
    }

    private static IServiceCollection AddHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PERF-09: Configure HttpClient with connection pooling optimizations
        services.AddHttpClient(); // Default client with pooling

        // Ollama client with optimized settings
        services.AddHttpClient("Ollama", client =>
        {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
            var ollamaUrl = configuration["OLLAMA_URL"] ?? "http://localhost:11434";
#pragma warning restore S1075
            client.BaseAddress = new Uri(ollamaUrl);
            var timeoutSeconds = configuration.GetValue<int>("AIAgents:DefaultTimeoutSeconds", 30);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2), // Recycle connections every 2 min
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1), // Close idle connections after 1 min
            MaxConnectionsPerServer = 20, // Allow up to 20 concurrent connections
            EnableMultipleHttp2Connections = true // Enable HTTP/2 multiplexing
        });

        // OpenRouter client with optimized settings (unified gateway for cloud AI models)
        // S1075: OpenRouter API endpoint (official public endpoint)
#pragma warning disable S1075 // URIs should not be hardcoded - Official API endpoint
        const string OpenRouterApiBaseUrl = "https://openrouter.ai/api/v1/";
#pragma warning restore S1075

        services.AddHttpClient("OpenRouter", client =>
        {
            client.BaseAddress = new Uri(OpenRouterApiBaseUrl);
            var timeoutSeconds = configuration.GetValue<int>("AIAgents:DefaultTimeoutSeconds", 30);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        // Issue #2520: Polly retry policy for transient failures (3 attempts with exponential backoff: 2s, 4s, 8s)
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        // Issue #2520: Circuit breaker policy (5 failures → open for 30s)
        .AddTransientHttpErrorPolicy(policy =>
            policy.CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30)))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        });

        // Qdrant HttpClient removed — pgvector is the sole vector store (no external Qdrant needed)

        // ADR-016 Phase 2: HuggingFace embedding client
        services.AddHttpClient("HuggingFace", client =>
        {
            // Base address will be set by HuggingFaceEmbeddingProvider based on config
            var timeoutSeconds = configuration.GetValue<int>("Embedding:TimeoutSeconds", 60);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        });

        // External embedding service client (Python microservice)
        services.AddHttpClient("EmbeddingService", client =>
        {
#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for external service
            var serviceUrl = configuration["LOCAL_EMBEDDING_URL"]
                ?? configuration["Embedding:LocalServiceUrl"]
                ?? "http://embedding-service:8000";
#pragma warning restore S1075
            client.BaseAddress = new Uri(serviceUrl);
            client.Timeout = TimeSpan.FromSeconds(300);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        });

        // Issue #3120: BoardGameGeek API client
        // Load BGG token first (needed for client configuration)
        var bggTokenForClient = SecretsHelper.GetSecretOrValue(
            configuration,
            "BGG_API_TOKEN",
            logger: null,
            required: false
        ) ?? Environment.GetEnvironmentVariable("BGG_API_TOKEN");

        services.AddHttpClient<Infrastructure.ExternalServices.BoardGameGeek.IBggApiClient,
            Infrastructure.ExternalServices.BoardGameGeek.BggApiClient>(client =>
        {
#pragma warning disable S1075 // URIs should not be hardcoded - Official BGG API endpoint
            client.BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/");
#pragma warning restore S1075
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI/1.0");

            // Add Bearer token if configured (REQUIRED as of Jan 2026)
            if (!string.IsNullOrWhiteSpace(bggTokenForClient))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bggTokenForClient}");
            }
        })
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        .AddTransientHttpErrorPolicy(policy =>
            policy.CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30)))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
            EnableMultipleHttp2Connections = true
        });

        // Named HttpClient "BggApi" used by BggApiService and BggApiHealthCheck
        // Must share the same Bearer token as the typed IBggApiClient above
        services.AddHttpClient("BggApi", client =>
        {
#pragma warning disable S1075 // URIs should not be hardcoded - Official BGG API endpoint
            client.BaseAddress = new Uri("https://boardgamegeek.com/xmlapi2/");
#pragma warning restore S1075
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI/1.0");

            if (!string.IsNullOrWhiteSpace(bggTokenForClient))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bggTokenForClient}");
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
            EnableMultipleHttp2Connections = true
        });

        // Configure BGG settings from appsettings.json
        services.Configure<BggConfiguration>(configuration.GetSection("Bgg"));

        return services;
    }

    private static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        // Time provider for testability
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    private static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        // Background task execution (fire-and-forget)
        services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

        // Background task orchestration with distributed coordination (Redis)
        services.AddSingleton<IBackgroundTaskOrchestrator, RedisBackgroundTaskOrchestrator>();

        // Issue #3541: BGG import queue service
        services.AddScoped<Infrastructure.Services.IBggImportQueueService, Infrastructure.Services.BggImportQueueService>();
        services.AddHostedService<Infrastructure.BackgroundServices.BggImportQueueBackgroundService>();

        // Admin Invitation Flow: background services for invitation lifecycle
        services.AddHostedService<Infrastructure.BackgroundServices.InvitationCleanupService>();
        services.AddHostedService<Infrastructure.BackgroundServices.GameSuggestionProcessorService>();

        // Issue #936: Infisical secrets management client (POC)
        services.AddHttpClient("Infisical", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10); // Secret fetch should be fast
            client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI/1.0 Infisical-POC");
        })
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(2, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        // ISSUE-3499: Orchestration service client for Tutor agent
        services.AddHttpClient("OrchestrationService", client =>
        {
#pragma warning disable S1075
            client.BaseAddress = new Uri("http://orchestration-service:8004");
#pragma warning restore S1075
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,
        });

        return services;
    }
}