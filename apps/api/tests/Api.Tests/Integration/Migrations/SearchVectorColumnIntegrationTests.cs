using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Migrations;

/// <summary>
/// #2022 — Verifies that the InitialCreate migration provisions the
/// <c>pgvector_embeddings.search_vector</c> tsvector column and its GIN index.
///
/// Background: the column is a Postgres GENERATED ALWAYS STORED column that the
/// runtime adapter (<see cref="Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.PgVectorStoreAdapter"/>,
/// line ~481) needs in order to create <c>idx_pgvector_embeddings_search_vector USING gin</c>.
/// EF cannot model GENERATED columns, so the migration must add it via raw SQL right
/// after the <c>CreateTable</c> of <c>pgvector_embeddings</c>; otherwise a fresh DB
/// boots with the table but no <c>search_vector</c>, and the adapter fails with
/// <c>42703: column "search_vector" does not exist</c>.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "2022")]
public sealed class SearchVectorColumnIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SearchVectorColumnIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        // Isolated database so we exercise the migration cleanly, without
        // interference from other tests in the same fixture group.
        _databaseName = $"test_searchvec_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply the InitialCreate migration. With retry to match the pattern used in
        // sibling integration tests (NpgsqlException retries from CI flake).
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }

    [Fact]
    public async Task InitialCreate_AddsSearchVectorColumnToPgvectorEmbeddings()
    {
        Assert.NotNull(_dbContext);
        var conn = (NpgsqlConnection)_dbContext!.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(TestCancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_name = 'pgvector_embeddings'
              AND column_name = 'search_vector';
        ";

        await using var reader = await cmd.ExecuteReaderAsync(TestCancellationToken);
        Assert.True(
            await reader.ReadAsync(TestCancellationToken),
            "search_vector column should exist on pgvector_embeddings after InitialCreate migration runs.");
        Assert.Equal("tsvector", reader["data_type"]);
    }

    [Fact]
    public async Task InitialCreate_CreatesGinIndexOnSearchVector()
    {
        Assert.NotNull(_dbContext);
        var conn = (NpgsqlConnection)_dbContext!.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(TestCancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 1
            FROM pg_indexes
            WHERE tablename = 'pgvector_embeddings'
              AND indexname = 'idx_pgvector_embeddings_search_vector';
        ";

        var result = await cmd.ExecuteScalarAsync(TestCancellationToken);
        Assert.NotNull(result);
    }
}
