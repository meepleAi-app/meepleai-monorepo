using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests using TestContainers with PostgreSQL (local) or service containers (CI)
/// </summary>
public abstract class PostgresIntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgresContainer;
    private readonly bool _isRunningInCi;
    private string _connectionString;

    protected MeepleAiDbContext DbContext { get; private set; } = null!;
    protected string ConnectionString => _connectionString;

    protected PostgresIntegrationTestBase()
    {
        // Detect CI environment
        _isRunningInCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (_isRunningInCi)
        {
            // In CI: Use service container from environment variable
            _connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ??
                               "Host=localhost;Port=5432;Database=meepleai_test;Username=meeple;Password=meeplepass";
            _postgresContainer = null;
        }
        else
        {
            // Local: Use TestContainers
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("meepleai_test")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .WithCleanUp(true)
                .Build();
            _connectionString = string.Empty; // Will be set after container starts
        }
    }

    public async Task InitializeAsync()
    {
        if (!_isRunningInCi && _postgresContainer != null)
        {
            // Start PostgreSQL container (local development only)
            await _postgresContainer.StartAsync();
            _connectionString = _postgresContainer.GetConnectionString();
        }

        // Create DbContext with PostgreSQL connection (works for both CI and local)
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
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

        if (_postgresContainer != null)
        {
            // Stop and remove container (local development only)
            await _postgresContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new scoped DbContext for background tasks
    /// </summary>
    protected MeepleAiDbContext CreateScopedDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new MeepleAiDbContext(options);
    }
}
