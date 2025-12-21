using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
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
            // Issue #2152: Debug ALL environment variables to find ConnectionStrings__Postgres
            Console.WriteLine("[DEBUG #2152] === Environment Variables Containing 'POSTGRES' ===");
            foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
            {
                var key = env.Key?.ToString() ?? "";
                if (key.Contains("POSTGRES", StringComparison.OrdinalIgnoreCase) || key.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
                {
                    var val = env.Value?.ToString() ?? "NULL";
                    var maskedVal = val.Length > 50 ? val.Substring(0, 50) + "..." : val;
                    Console.WriteLine($"  {key} = {maskedVal}");
                }
            }
            Console.WriteLine("[DEBUG #2152] ===================================================");

            // Issue #2152: Read ConnectionStrings__Postgres directly from env var to bypass ALL config caching
            var envVarConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
            Console.WriteLine($"[DEBUG #2152] Environment.GetEnvironmentVariable('ConnectionStrings__Postgres'): {(envVarConnectionString != null ? envVarConnectionString.Substring(0, Math.Min(80, envVarConnectionString.Length)) : "NULL")}");

            // SEC-708: Build connection string from Docker Secrets if available
            var connectionString = envVarConnectionString
                ?? configuration["ConnectionStrings__Postgres"]
                ?? configuration.GetConnectionString("Postgres")
                ?? SecretsHelper.BuildPostgresConnectionString(configuration);

            Console.WriteLine($"[DEBUG #2152] FINAL connectionString source: {(envVarConnectionString != null ? "Environment.GetEnvironmentVariable" : configuration["ConnectionStrings__Postgres"] != null ? "configuration[]" : configuration.GetConnectionString("Postgres") != null ? "GetConnectionString" : "SecretsHelper")}");

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
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        var redisUrl = configuration["REDIS_URL"] ?? "localhost:6379";
#pragma warning restore S1075
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(redisUrl);

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
            logger.LogInformation("Connecting to Redis at {RedisUrl} with optimized settings", redisUrl);

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
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisUrl;
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
        services.AddScoped<IHybridCacheService, HybridCacheService>();

        // AI-10: Cache optimization services
        services.Configure<CacheOptimizationConfiguration>(
            configuration.GetSection("CacheOptimization"));
        services.AddSingleton<ICacheMetricsRecorder, CacheMetricsRecorder>();
        services.AddSingleton<IDynamicTtlStrategy, DynamicTtlStrategy>();
        services.AddSingleton<IRedisFrequencyTracker, RedisFrequencyTracker>();

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
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        });

        // Qdrant client with optimized settings
        services.AddHttpClient("Qdrant", client =>
        {
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
            var qdrantUrl = configuration["QdrantUrl"] ?? "http://localhost:6333";
#pragma warning restore S1075
            client.BaseAddress = new Uri(qdrantUrl);
            var timeoutSeconds = configuration.GetValue<int>("AIAgents:DefaultTimeoutSeconds", 30);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 30, // Higher for vector DB operations
            EnableMultipleHttp2Connections = true
        });

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

        // AI-13: BoardGameGeek API client with retry logic and connection pooling
        services.Configure<BggConfiguration>(configuration.GetSection("Bgg"));
        services.AddHttpClient("BggApi", (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<BggConfiguration>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI/1.0 (https://meepleai.dev)");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 5, // BGG rate limit: max 2 req/s
            EnableMultipleHttp2Connections = false // BGG doesn't support HTTP/2
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<BggConfiguration>>().Value;
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    config.RetryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(config.RetryDelaySeconds, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning("BGG API retry {RetryCount}/{MaxRetries}. Waiting {Delay}ms before next attempt",
                            retryCount, config.RetryCount, timespan.TotalMilliseconds);
                    });
        });

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

        // Issue #936: Infisical secrets management client (POC)
        services.AddHttpClient("Infisical", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10); // Secret fetch should be fast
            client.DefaultRequestHeaders.Add("User-Agent", "MeepleAI/1.0 Infisical-POC");
        })
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(2, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        return services;
    }
}
