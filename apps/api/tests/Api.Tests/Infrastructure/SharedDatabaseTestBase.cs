using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.TestHelpers;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base class for integration tests using shared Testcontainers.
/// Provides standardized setup with isolated databases and optional transaction-based cleanup.
///
/// Issue #2541: Reduces test execution time by sharing PostgreSQL container across tests.
/// - Container startup: ~350s → ~35s (90% reduction)
/// - Database migrations: ~80s → ~8s (90% reduction)
/// - Total test time: 11+ min → <3 min target (73% improvement)
///
/// Usage:
/// [Collection("SharedTestcontainers")]
/// public class YourRepositoryTests : SharedDatabaseTestBase
/// {
///     public YourRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture) { }
///
///     [Fact]
///     public async Task YourTest()
///     {
///         // DbContext and Mediator are ready to use
///         var entity = new YourEntity();
///         await DbContext.AddAsync(entity);
///         await DbContext.SaveChangesAsync();
///     }
/// }
/// </summary>
public abstract class SharedDatabaseTestBase : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private IDbContextTransaction? _transaction;

    // Issue #2577: Global lock for EF Core migrations to prevent race conditions
    // When 34+ test classes run in parallel, concurrent MigrateAsync() calls can cause
    // "column already exists" errors even with isolated databases.
    // Issue #CI-Optimization: Increased to 4 concurrent migrations to support parallel
    // collection groups while still preventing metadata conflicts.
    private static readonly SemaphoreSlim MigrationLock = new(4, 4);

    /// <summary>
    /// Database context for test operations.
    /// Automatically configured and migrated.
    /// </summary>
    protected MeepleAiDbContext DbContext { get; private set; } = null!;

    /// <summary>
    /// MediatR instance for command/query testing.
    /// Configured with all handlers from API assembly.
    /// </summary>
    protected IMediator Mediator { get; private set; } = null!;

    /// <summary>
    /// Test time provider for time-dependent testing.
    /// Allows manipulation of time in tests (e.g., for expiration testing).
    /// </summary>
    protected TestTimeProvider TimeProvider { get; private set; } = null!;

    /// <summary>
    /// Whether to use transaction-based isolation (default: false).
    /// When true, each test runs in a transaction that is rolled back on cleanup.
    /// When false, uses database truncation for cleanup (faster for most scenarios).
    /// </summary>
    protected virtual bool UseTransactionIsolation => false;

    protected SharedDatabaseTestBase(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Initialize test time provider
        TimeProvider = new TestTimeProvider();

        // Create unique database name for this test class
        _databaseName = $"test_{GetType().Name.ToLowerInvariant()}_{Guid.NewGuid():N}";

        // Create isolated database and get connection string
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Create and configure DbContext
        DbContext = _fixture.CreateDbContext(_connectionString);

        // Issue #2577: Use idempotent migration check instead of lock
        // EF Core's MigrateAsync() can cause "column already exists" errors when:
        // - Multiple test classes migrate concurrently (even to isolated databases)
        // - Connection pooling shares metadata between contexts
        // Solution: Check for pending migrations before applying
        await MigrationLock.WaitAsync();
        try
        {
            var pendingMigrations = await DbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await DbContext.Database.MigrateAsync();
            }
        }
        finally
        {
            MigrationLock.Release();
        }

        // Create MediatR instance
        Mediator = _fixture.CreateMediator();

        // Start transaction if isolation is enabled
        if (UseTransactionIsolation)
        {
            _transaction = await DbContext.Database.BeginTransactionAsync();
        }

        // Call derived class initialization hook
        await OnInitializedAsync();
    }

    /// <summary>
    /// Hook for derived classes to perform additional initialization after base setup.
    /// Override this method to add custom initialization logic.
    /// </summary>
    protected virtual ValueTask OnInitializedAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        // Rollback transaction if used
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        // Dispose DbContext
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }

        // Drop isolated database
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    /// <summary>
    /// Resets database to clean state for the next test.
    /// Only needed when NOT using transaction isolation.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        if (UseTransactionIsolation)
        {
            throw new InvalidOperationException(
                "ResetDatabaseAsync should not be called when UseTransactionIsolation is true. " +
                "Transaction rollback handles cleanup automatically.");
        }

        // Get all table names
        var tableNames = await DbContext.Database
            .SqlQueryRaw<string>(@"
                SELECT tablename
                FROM pg_tables
                WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync();

        if (tableNames.Count == 0)
        {
            return; // No tables to truncate
        }

        // Disable FK constraints temporarily
        await DbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';");

        // Truncate all tables with CASCADE
        foreach (var tableName in tableNames)
        {
            await DbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE;");
        }

        // Re-enable FK constraints
        await DbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';");
    }

    /// <summary>
    /// Creates an independent DbContext for concurrent operations.
    /// Useful for testing optimistic concurrency or multi-user scenarios.
    /// </summary>
    protected MeepleAiDbContext CreateIndependentDbContext()
    {
        return _fixture.CreateDbContext(_connectionString);
    }
}

/// <summary>
/// Generic base class for repository integration tests using shared Testcontainers.
/// Provides standardized setup with isolated databases and repository instance management.
///
/// Issue #2541: Replaces IntegrationTestBase<T> with shared container approach.
/// Reduces container startup overhead from ~350s to ~35s (90% improvement).
///
/// Usage:
/// [Collection("SharedTestcontainers")]
/// public class UserRepositoryTests : SharedDatabaseTestBase<UserRepository>
/// {
///     public UserRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture) { }
///
///     protected override UserRepository CreateRepository(MeepleAiDbContext dbContext)
///         => new UserRepository(dbContext, MockEventCollector.Object);
///
///     [Fact]
///     public async Task YourTest()
///     {
///         // Repository, DbContext, and Mediator are ready to use
///         await Repository.AddAsync(entity);
///     }
/// }
/// </summary>
public abstract class SharedDatabaseTestBase<TRepository> : SharedDatabaseTestBase
    where TRepository : class
{
    private TRepository? _repository;
    private Moq.Mock<IDomainEventCollector>? _mockEventCollector;

    /// <summary>
    /// Repository instance for test operations.
    /// Automatically created via CreateRepository method.
    /// </summary>
    protected TRepository Repository
    {
        get
        {
            if (_repository == null)
            {
                throw new InvalidOperationException(
                    "Repository not initialized. Ensure InitializeAsync has been called.");
            }
            return _repository;
        }
        private set => _repository = value;
    }

    /// <summary>
    /// Mock domain event collector for testing.
    /// Available for repository creation that requires event collector.
    /// </summary>
    protected Moq.Mock<IDomainEventCollector> MockEventCollector
    {
        get
        {
            _mockEventCollector ??= new Moq.Mock<IDomainEventCollector>();
            return _mockEventCollector;
        }
    }

    protected SharedDatabaseTestBase(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    /// Creates repository instance for testing.
    /// Implement this method in derived test classes to provide repository configuration.
    /// </summary>
    /// <param name="dbContext">Database context for repository</param>
    /// <returns>Configured repository instance</returns>
    protected abstract TRepository CreateRepository(MeepleAiDbContext dbContext);

    /// <summary>
    /// Initializes repository instance after base infrastructure is set up.
    /// </summary>
    protected override async ValueTask OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Create repository after DbContext is initialized
        Repository = CreateRepository(DbContext);
    }

    /// <summary>
    /// Creates an independent repository instance for concurrent operations.
    /// Useful for testing concurrent access or multi-user scenarios.
    /// </summary>
    protected TRepository CreateIndependentRepository()
    {
        var independentDbContext = CreateIndependentDbContext();
        return CreateRepository(independentDbContext);
    }
}
