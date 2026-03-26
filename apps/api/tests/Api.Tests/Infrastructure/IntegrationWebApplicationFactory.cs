using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Pgvector.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Shared WebApplicationFactory for integration tests.
/// Provides complete, isolated DI/config setup that works in both local and CI environments.
///
/// Key features:
/// - Config isolation via Sources.Clear() (prevents CI env vars from overriding test config)
/// - Explicit DbContext registration with test connection string
/// - Hosted service removal (prevents background service startup failures)
/// - Mocks for Redis, embedding, hybrid cache
/// - IDomainEventCollector registration
///
/// Modeled after the working E2EWebApplicationFactory and ReviewLockEndpointsIntegrationTests patterns.
/// </summary>
internal static class IntegrationWebApplicationFactory
{
    /// <summary>
    /// Creates a fully configured WebApplicationFactory for integration tests.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string for the isolated test database.</param>
    /// <param name="redisConnectionString">Optional Redis connection string. If null, Redis is mocked.</param>
    /// <param name="extraConfig">Optional extra configuration keys to add beyond the defaults.</param>
    /// <returns>A configured WebApplicationFactory ready for creating HttpClient instances.</returns>
    public static WebApplicationFactory<Program> Create(
        string connectionString,
        string? redisConnectionString = null,
        Dictionary<string, string?>? extraConfig = null)
    {
        // Must be set before factory creation — middleware checks these env vars directly
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    // Clear ALL existing sources to prevent CI env vars (ConnectionStrings__Postgres)
                    // from overriding the test's connection string — root cause of 403 errors in CI
                    configBuilder.Sources.Clear();

                    var config = new Dictionary<string, string?>
                    {
                        // Database — use ConnectionStrings:DefaultConnection for the explicit DbContext registration below
                        ["ConnectionStrings:DefaultConnection"] = connectionString,
                        // Also set ConnectionStrings:Postgres in case any code reads it directly
                        ["ConnectionStrings:Postgres"] = connectionString,
                        // JWT (required for session authentication)
                        ["Jwt:Secret"] = "test-secret-key-for-integration-tests-minimum-32-characters-long",
                        ["Jwt:Issuer"] = "MeepleAI-Test",
                        ["Jwt:Audience"] = "MeepleAI-Test",
                        // OpenRouter
                        ["OpenRouter:ApiKey"] = "test-key",
                        ["OpenRouter:BaseUrl"] = "https://test.local",
                        // Disable external services
                        ["BoardGameGeek:Enabled"] = "false",
                        ["Embedding:Enabled"] = "false",
                        ["Embedding:Url"] = "http://localhost:8000",
                        ["Qdrant:Enabled"] = "false",
                        ["Qdrant:Host"] = "localhost",
                        ["Qdrant:Port"] = "6333",
                        // Redis
                        ["Redis:Enabled"] = redisConnectionString != null ? "true" : "false",
                        ["Redis:ConnectionString"] = redisConnectionString ?? "localhost:6379",
                        // Session
                        ["Authentication:SessionManagement:SessionExpirationDays"] = "30",
                        // Admin seeding
                        ["Admin:Email"] = "admin@test.local",
                        ["Admin:Password"] = "TestAdmin123!",
                        ["Admin:DisplayName"] = "Test Admin",
                        ["INITIAL_ADMIN_EMAIL"] = "admin@test.local",
                        ["INITIAL_ADMIN_PASSWORD"] = "TestAdmin123!",
                        ["INITIAL_ADMIN_DISPLAY_NAME"] = "Test Admin",
                        // Observability
                        ["Observability:Enabled"] = "false",
                        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
                        // Rate limiting
                        ["RateLimiting:Enabled"] = "false"
                    };

                    // Merge extra config if provided
                    if (extraConfig != null)
                    {
                        foreach (var kvp in extraConfig)
                        {
                            config[kvp.Key] = kvp.Value;
                        }
                    }

                    configBuilder.AddInMemoryCollection(config);
                });

                // ConfigureServices (NOT ConfigureTestServices) — runs before IStartupFilter
                builder.ConfigureServices(services =>
                {
                    // Remove all hosted services to prevent background service startup failures
                    var hostedServiceDescriptors = services
                        .Where(d => d.ServiceType == typeof(IHostedService))
                        .ToList();
                    foreach (var descriptor in hostedServiceDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    // Remove and re-register DbContext with test connection string
                    services.RemoveAll<DbContextOptions<MeepleAiDbContext>>();
                    services.RemoveAll<MeepleAiDbContext>();
                    services.RemoveAll<IDomainEventCollector>();

                    services.AddScoped<IDomainEventCollector, DomainEventCollector>();

                    services.AddDbContext<MeepleAiDbContext>((serviceProvider, options) =>
                    {
                        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                        var connStr = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("DefaultConnection not configured");

                        options.UseNpgsql(connStr, o => o.UseVector());
                        options.EnableSensitiveDataLogging();
                        options.ConfigureWarnings(warnings =>
                            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });
                });

                // ConfigureTestServices — runs after all other service configuration
                builder.ConfigureTestServices(services =>
                {
                    // Mock Redis (IConnectionMultiplexer) with proper IDatabase mock
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    var mockDatabase = new Mock<IDatabase>();
                    mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                        .Returns(mockDatabase.Object);
                    services.AddSingleton(mockRedis.Object);

                    // Mock embedding service
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());

                    // Mock HybridCache — use pass-through implementation that executes factory directly
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService, TestHybridCacheService>();
                });
            });
    }
}

/// <summary>
/// Pass-through IHybridCacheService implementation for integration tests.
/// Executes factory functions directly without caching.
/// </summary>
internal sealed class TestHybridCacheService : Api.Services.IHybridCacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
    {
        return await factory(ct).ConfigureAwait(false);
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;

    public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

    public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);

    public Task<Api.Services.HybridCacheStats> GetStatsAsync(CancellationToken ct = default)
        => Task.FromResult(new Api.Services.HybridCacheStats());
}
