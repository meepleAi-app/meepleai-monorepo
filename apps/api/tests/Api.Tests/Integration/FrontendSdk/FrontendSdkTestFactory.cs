using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
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
    private PostgreSqlContainer? _postgresContainer;
    private string? _connectionString;

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
            // Create Testcontainer for local development
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("frontend_sdk_test")
                .WithUsername("testuser")
                .WithPassword("testpass")
                .WithPortBinding(5432, assignRandomHostPort: true)
                .Build();

            await _postgresContainer.StartAsync();
            _connectionString = _postgresContainer.GetConnectionString();
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
            .UseNpgsql(_connectionString)
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
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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
                options.UseNpgsql(_connectionString);
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
            services.RemoveAll(typeof(IQdrantService));
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IQdrantService>(_ => Mock.Of<IQdrantService>());
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
