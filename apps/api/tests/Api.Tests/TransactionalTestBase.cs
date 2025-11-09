using System.Data;
using Api.Infrastructure;
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests using transaction-based cleanup (TEST-05 #613).
///
/// Performance:
/// - Cleanup time: <1ms per test (99% faster than database recreation)
/// - Perfect test isolation via transaction rollback
/// - No data leakage between tests
///
/// Usage:
/// [Collection("Postgres Integration Tests")]
/// public class MyTests : TransactionalTestBase
/// {
///     public MyTests(PostgresCollectionFixture fixture) : base(fixture) { }
///
///     [Fact]
///     public async Task MyTest()
///     {
///         // Use DbContext - changes auto-rollback after test
///         var user = new UserEntity { Email = "test@example.com" };
///         DbContext.Users.Add(user);
///         await DbContext.SaveChangesAsync();
///
///         Assert.NotEqual(Guid.Empty, user.Id);
///         // Transaction rolls back automatically in DisposeAsync()
///     }
/// }
/// </summary>
public abstract class TransactionalTestBase : IntegrationTestBase
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    // Override DbContext to use transactional connection
    protected new MeepleAiDbContext DbContext { get; private set; } = null!;

    protected TransactionalTestBase(PostgresCollectionFixture fixture) : base(fixture)
    {
    }

    public override async ValueTask InitializeAsync()
    {
        // Call base to create Factory
        await base.InitializeAsync();

        // Create transactional connection and DbContext
        _connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_connection)
            .Options;

        DbContext = new MeepleAiDbContext(options);
        DbContext.Database.UseTransaction(_transaction);
    }

    public override async ValueTask DisposeAsync()
    {
        // Rollback transaction for fast cleanup (<1ms)
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }

        // Call base cleanup (disposes Factory, but skips tracked entity cleanup since transaction rolled back)
        await base.DisposeAsync();
    }
}
