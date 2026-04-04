using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Services;
using Api.Tests.Infrastructure;
using Api.SharedKernel.Application.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using Pgvector.EntityFrameworkCore; // Issue #3547: Enable pgvector type mapping
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.FrontendSdk;

/// <summary>
/// WebApplicationFactory for Frontend SDK integration tests.
///
/// Provides a TestServer that simulates real API behavior for testing
/// scenarios that the frontend SDK encounters:
/// - HTTP retry logic (503, 429, timeouts)
/// - Error handling (4xx, 5xx responses)
/// - Authentication flows (sessions, API keys, 2FA, OAuth)
/// - Request deduplication
/// - Circuit breaker behavior
///
/// Uses Testcontainers for real database to ensure accurate
/// integration testing of the full request pipeline.
/// </summary>
public class FrontendSdkTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private string? _connectionString;

    /// <summary>
    /// Issue #3102: Static constructor ensures DISABLE_RATE_LIMITING is set BEFORE
    /// WebApplicationFactory creates the host. This is critical because AddRateLimitingServices
    /// reads this env var during service registration, which happens before ConfigureWebHost.
    /// </summary>
    static FrontendSdkTestFactory()
    {
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");
    }

    /// <summary>
    /// Initialize PostgreSQL container before tests.
    /// Container is shared across all tests in the collection for performance.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        // Check if external connection string is provided (for CI/CD)
        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            _connectionString = externalConn;
        }
        else
        {
            // Issue #2031 fix: Use ContainerBuilder instead of PostgreSqlBuilder
            // to avoid exec-based wait strategy that causes "cannot hijack" errors
            _postgresContainer = new ContainerBuilder()
                .WithImage("pgvector/pgvector:pg16")  // Issue #3547: Use pgvector image for Vector column support
                .WithEnvironment("POSTGRES_USER", "testuser")
                .WithEnvironment("POSTGRES_PASSWORD", "testpass")
                .WithEnvironment("POSTGRES_DB", "frontend_sdk_test")
                .WithPortBinding(5432, assignRandomHostPort: true)
                .WithCleanUp(true)
                .Build();

            await _postgresContainer.StartAsync();

            var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
            // Issue #2577: Enable connection pooling to prevent timeout failures
            // Same optimization as SharedTestcontainersFixture for consistency
            _connectionString = $"Host=localhost;Port={postgresPort};Database=frontend_sdk_test;Username=testuser;Password=testpass;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=10;Pooling=true;MinPoolSize=2;MaxPoolSize=50;Timeout=30;CommandTimeout=60;ConnectionIdleLifetime=60;ConnectionPruningInterval=10;";

            // Issue #2031: Wait for PostgreSQL to accept connections with retry
            await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        }

        // Initialize database schema with migrations
        await InitializeDatabaseAsync();
    }

    /// <summary>
    /// Initialize database schema by running EF migrations.
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString, o => o.UseVector()) // Issue #3547: Enable pgvector type mapping
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        await using var context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        // Ensure database is empty and run all migrations
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Cleanup PostgreSQL container after all tests complete.
    /// </summary>
    public new async ValueTask DisposeAsync()
    {
        // Clear environment variable set during configuration
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", null);

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Issue #2705: Disable custom RateLimitingMiddleware via environment variable
        // Issue #3102: Note - the static constructor sets this BEFORE host creation
        // for the built-in rate limiter. This line ensures it's set for the custom middleware.
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            // Provide test configuration
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-openrouter-key",
                ["OPENROUTER_API_KEY_FILE"] = null,
                ["ConnectionStrings:Postgres"] = _connectionString,
                // Disable health checks for external services in tests
                ["HealthChecks:Qdrant:Enabled"] = "false",
                ["HealthChecks:Redis:Enabled"] = "false",
                // Issue #2705: Disable rate limiting for integration tests to prevent 429 errors
                ["RateLimiting:Enabled"] = "false",
                // Configure Redis/Qdrant URLs to dummy values (services are mocked)
                ["REDIS_URL"] = "localhost:6379",
                ["QDRANT_URL"] = "http://localhost:6333",
                // Enable retry and circuit breaker for testing
                ["RetryPolicy:MaxRetries"] = "3",
                ["RetryPolicy:InitialDelayMs"] = "100",
                ["CircuitBreaker:FailureThreshold"] = "3",
                ["CircuitBreaker:ResetTimeoutSeconds"] = "5",
                // Configure CORS for tests
                ["Cors:Origins:0"] = "http://localhost:3000",
                ["Cors:Origins:1"] = "http://localhost:3001",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace DbContext with test database
            services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
            services.RemoveAll(typeof(MeepleAiDbContext));

            services.AddDbContext<MeepleAiDbContext>(options =>
            {
                options.UseNpgsql(_connectionString, o => o.UseVector()); // Issue #3547: Enable pgvector type mapping
                options.EnableSensitiveDataLogging(); // Better error messages
            });

            // Mock external services that aren't needed for SDK tests
            services.RemoveAll(typeof(IConnectionMultiplexer));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // Mock vector/embedding services (not needed for HTTP/auth tests)
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IEmbeddingService>(_ => Mock.Of<IEmbeddingService>());

            // Mock cache service
            services.RemoveAll(typeof(IHybridCacheService));
            services.AddScoped<IHybridCacheService>(_ => Mock.Of<IHybridCacheService>());

            // Ensure domain event collector is registered
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        });

        builder.ConfigureTestServices(services =>
        {
            // Additional test-specific service configuration can go here
        });
    }
}

/// <summary>
/// xUnit collection definition for sharing FrontendSdkTestFactory across test classes.
/// This improves performance by reusing the TestServer and database container.
/// </summary>
[CollectionDefinition(nameof(FrontendSdkTestCollection))]
public class FrontendSdkTestCollection : ICollectionFixture<FrontendSdkTestFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
