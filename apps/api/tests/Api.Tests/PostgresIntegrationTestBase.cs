using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests using TestContainers with PostgreSQL
/// </summary>
public abstract class PostgresIntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    protected MeepleAiDbContext DbContext { get; private set; } = null!;
    protected string ConnectionString => _postgresContainer.GetConnectionString();

    protected PostgresIntegrationTestBase()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        await _postgresContainer.StartAsync();

        // Create DbContext with real PostgreSQL connection
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        DbContext = new MeepleAiDbContext(options);

        // Apply migrations to create schema
        await DbContext.Database.MigrateAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Dispose DbContext
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }

        // Stop and remove container
        await _postgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Creates a new scoped DbContext for background tasks
    /// </summary>
    protected MeepleAiDbContext CreateScopedDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new MeepleAiDbContext(options);
    }
}
