using System.Diagnostics;
using Api.Infrastructure;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.Fixtures;

/// <summary>
/// Shared Postgres Testcontainer fixture for all integration tests.
/// Implements CollectionFixture pattern for optimal performance (TEST-01 #609).
///
/// Performance:
/// - Single container startup for entire test collection (~5s one-time overhead)
/// - Replaces per-class containers (eliminates 16 × 3-5s = 48-80s startup time)
/// - Expected improvement: 80-90% faster test execution
///
/// Migration Strategy (Issue #598):
/// - Replaces SQLite in-memory database with production-parity Postgres
/// - Fixes FK constraint issues (Postgres handles cascades correctly)
/// - Eliminates type mismatch errors (JSONB, arrays, UUIDs)
/// - Resolves 469 test failures from SQLite/Postgres incompatibility
/// </summary>
public class PostgresCollectionFixture : IAsyncLifetime
{
    private readonly Stopwatch _stopwatch = new();

    /// <summary>
    /// Postgres container instance (postgres:15-alpine for 75% smaller image)
    /// </summary>
    public PostgreSqlContainer PostgresContainer { get; private set; } = null!;

    /// <summary>
    /// Postgres connection string for DbContext creation
    /// </summary>
    public string ConnectionString => PostgresContainer.GetConnectionString();

    /// <summary>
    /// Configuration for tests (includes connection string override)
    /// </summary>
    public IConfiguration Configuration { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Console.WriteLine("🚀 [PostgresCollectionFixture] Starting Postgres container...");
        _stopwatch.Start();

        await StartPostgresAsync();

        _stopwatch.Stop();
        Console.WriteLine($"✅ [PostgresCollectionFixture] Container started in {_stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"📍 [PostgresCollectionFixture] Connection: {ConnectionString}");

        // Build configuration with Postgres connection string
        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = ConnectionString,
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            })
            .Build();

        // Run migrations once for the entire test collection (performance optimization)
        Console.WriteLine("🔧 [PostgresCollectionFixture] Running database migrations...");
        await EnsureDatabaseMigratedAsync();
    }

    private async Task StartPostgresAsync()
    {
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("meepleai_test")
            .WithUsername("postgres")
            .WithPassword("testpassword")
            .Build();

        await PostgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        Console.WriteLine("🧹 [PostgresCollectionFixture] Cleaning up Postgres container...");

        if (PostgresContainer != null)
        {
            await PostgresContainer.DisposeAsync();
        }

        Console.WriteLine("✅ [PostgresCollectionFixture] Cleanup complete");
    }

    /// <summary>
    /// Creates a new DbContext instance connected to the test Postgres container.
    /// Each test should create its own DbContext for isolation.
    /// </summary>
    public MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        var context = new MeepleAiDbContext(options);
        return context;
    }

    /// <summary>
    /// Ensures database is created and migrated (run once per fixture)
    /// </summary>
    public async Task EnsureDatabaseMigratedAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
        Console.WriteLine("✅ [PostgresCollectionFixture] Database migrated successfully");
    }
}

/// <summary>
/// Collection definition for all integration tests using Postgres.
/// All test classes marked with [Collection("Postgres Integration Tests")]
/// will share the same PostgresCollectionFixture instance.
/// </summary>
[CollectionDefinition("Postgres Integration Tests")]
public class PostgresIntegrationTestCollection : ICollectionFixture<PostgresCollectionFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition]
    // and the ICollectionFixture<> interface.
}
