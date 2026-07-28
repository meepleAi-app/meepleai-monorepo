using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Migrations;

/// <summary>
/// #3336 — Verifies that migrations provision the italian expression GIN index
/// <c>ix_text_chunks_content_tsv_italian</c> on <c>text_chunks</c>.
///
/// Background: <c>text_chunks.search_vector</c> is a GENERATED english-only tsvector column, so the
/// non-english keyword path in <see cref="Api.Services.KeywordSearchService"/> matches against a
/// query-time <c>to_tsvector('italian'::regconfig, "Content")</c> that had no supporting index (a
/// per-row seq-scan after the GameId bitmap). This raw-SQL GIN index restores a GIN lookup for the
/// dominant non-english language. Because an expression GIN index is invisible to the EF model, a
/// future <c>ef migrations add Initial</c> flatten would silently drop it (see #2875); this test is
/// the regression guard — the same role SearchVectorColumnIntegrationTests plays for search_vector.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3336")]
public sealed class ItalianContentFtsIndexIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ItalianContentFtsIndexIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_itfts_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

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
    public async Task Migration_CreatesItalianContentGinIndex()
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
            WHERE tablename = 'text_chunks'
              AND indexname = 'ix_text_chunks_content_tsv_italian';";

        var result = await cmd.ExecuteScalarAsync(TestCancellationToken);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ItalianContentGinIndex_IsGinOverItalianToTsvector()
    {
        Assert.NotNull(_dbContext);
        var conn = (NpgsqlConnection)_dbContext!.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(TestCancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT indexdef
            FROM pg_indexes
            WHERE tablename = 'text_chunks'
              AND indexname = 'ix_text_chunks_content_tsv_italian';";

        var indexDef = (string?)await cmd.ExecuteScalarAsync(TestCancellationToken);

        Assert.False(string.IsNullOrEmpty(indexDef), "italian content GIN index should exist.");
        // Guard the exact shape the KeywordSearchService query relies on: a GIN index over the
        // italian to_tsvector of the Content column. An english/other-config index would not be
        // used by the italian query, silently regressing back to the seq-scan.
        Assert.Contains("gin", indexDef!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Content", indexDef!, StringComparison.Ordinal);
        // Couple the index to the EXACT config KeywordSearchService.ResolveFtsConfig("it") emits
        // ("italian") — asserted as the contiguous `to_tsvector('italian'` expression, not loose
        // substrings. If the index config ever drifts from what the service emits (e.g. a future
        // custom 'meepleai_italian' config), the planner stops using this index and Italian keyword
        // search silently regresses to the per-row seq-scan; this assertion fails loudly instead.
        Assert.Contains("to_tsvector('italian'", indexDef!, StringComparison.Ordinal);
    }
}
