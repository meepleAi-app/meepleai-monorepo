using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;
using Npgsql;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests using Testcontainers with PostgreSQL.
/// Ensures complete test isolation by resetting database state between each test.
/// </summary>
public abstract class IntegrationTestBase<TRepository> : IAsyncLifetime
    where TRepository : class
{
    private PostgreSqlContainer? _postgresContainer;
    private string? _connectionString;

    protected MeepleAiDbContext DbContext { get; private set; } = null!;
    protected TRepository Repository { get; private set; } = null!;
    protected TestTimeProvider TimeProvider { get; private set; } = null!;
    protected Mock<IDomainEventCollector> MockEventCollector { get; private set; } = null!;

    /// <summary>
    /// Database name for the test container (unique per test class to avoid conflicts).
    /// </summary>
    protected abstract string DatabaseName { get; }

    /// <summary>
    /// Factory method to create the repository instance with required dependencies.
    /// </summary>
    protected abstract TRepository CreateRepository(MeepleAiDbContext dbContext);

    /// <summary>
    /// Initialize PostgreSQL container and DbContext once per test class.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        TimeProvider = new TestTimeProvider();

        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            var builder = new NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = DatabaseName,
                SslMode = SslMode.Disable,
                KeepAlive = 30,
                Pooling = false
            };
            _connectionString = builder.ConnectionString;
        }
        else
        {
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase(DatabaseName)
                .WithUsername("testuser")
                .WithPassword("testpass")
                .WithPortBinding(5432, assignRandomHostPort: true)
                .Build();

            await _postgresContainer.StartAsync();

            _connectionString = _postgresContainer.GetConnectionString();
        }

        // Create initial DbContext to run migrations
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        using (var context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object))
        {
            // Make sure we start from a pristine schema for every test run
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
        }

        // Create fresh DbContext and Repository for first test
        CreateFreshDbContext();
    }

    /// <summary>
    /// Dispose PostgreSQL container and DbContext after all tests complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a fresh DbContext and Repository instance.
    /// Call this after ResetDatabaseAsync() to ensure clean state for each test.
    /// </summary>
    private void CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .EnableSensitiveDataLogging() // For better error messages
            .Options;

        var mockMediator = new Mock<IMediator>();
        MockEventCollector = new Mock<IDomainEventCollector>();
        MockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        DbContext = new MeepleAiDbContext(options, mockMediator.Object, MockEventCollector.Object);
        Repository = CreateRepository(DbContext);
    }

    /// <summary>
    /// Creates an independent DbContext instance for concurrent operations.
    /// Use this in tests that run multiple operations in parallel.
    /// </summary>
    protected MeepleAiDbContext CreateIndependentDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .EnableSensitiveDataLogging()
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    /// <summary>
    /// Creates an independent repository instance with its own DbContext for concurrent operations.
    /// Dispose the returned repository when done to release resources.
    /// </summary>
    protected TRepository CreateIndependentRepository()
    {
        var dbContext = CreateIndependentDbContext();
        return CreateRepository(dbContext);
    }

    /// <summary>
    /// Clears all data from database tables and creates a fresh DbContext to ensure complete test isolation.
    /// Call this at the start of each test that assumes an empty database.
    ///
    /// Performance: Faster than recreating container (~10ms vs ~2000ms).
    /// Creates new DbContext instance to prevent concurrency issues.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        // Dispose old DbContext to prevent resource leaks
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }

        // Create temporary DbContext for cleanup
        var tempOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        await using (var tempContext = new MeepleAiDbContext(tempOptions, mockMediator.Object, mockEventCollector.Object))
        {
            // Get all table names dynamically from the database
            var tableNames = await tempContext.Database
                .SqlQueryRaw<string>(
                    @"SELECT tablename
                      FROM pg_tables
                      WHERE schemaname = 'public'
                      AND tablename != '__EFMigrationsHistory'")
                .ToListAsync();

            if (tableNames.Count > 0)
            {
                // Disable foreign key constraints temporarily for cleanup
                await tempContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';");

                try
                {
                    // Truncate all tables with CASCADE to handle FK dependencies
                    foreach (var tableName in tableNames)
                    {
                        await tempContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE;");
                    }
                }
                finally
                {
                    // Re-enable foreign key constraints
                    await tempContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';");
                }
            }
        }

        // Create fresh DbContext and Repository for the test
        CreateFreshDbContext();
    }
}

