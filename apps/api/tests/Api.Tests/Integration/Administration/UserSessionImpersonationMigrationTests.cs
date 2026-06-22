using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration test that verifies the AddImpersonationToUserSession migration applies cleanly
/// and produces the expected schema: 2 new nullable columns on user_sessions + partial index.
///
/// SP5 Admin Security S2 — Task 1 migration smoke test.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class UserSessionImpersonationMigrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UserSessionImpersonationMigrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_user_session_impersonation_{Guid.NewGuid():N}";
        _isolatedConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedConnectionString);
        var sp = services.BuildServiceProvider();
        _dbContext = sp.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext!.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    [Fact]
    public async Task AddImpersonationToUserSession_AddsBothNullableColumns()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'user_sessions'
              AND column_name IN ('impersonated_by_user_id', 'impersonated_until')
            ORDER BY column_name;
            """;

        var columns = new Dictionary<string, (string DataType, string IsNullable)>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(TestCancellationToken);
        while (await reader.ReadAsync(TestCancellationToken))
        {
            columns[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2));
        }

        columns.Should().ContainKey("impersonated_by_user_id",
            because: "AddImpersonationToUserSession migration must add impersonated_by_user_id");
        columns["impersonated_by_user_id"].DataType.Should().Be("uuid",
            because: "impersonated_by_user_id must be a UUID (Postgres native uuid type for Guid)");
        columns["impersonated_by_user_id"].IsNullable.Should().Be("YES",
            because: "impersonated_by_user_id must be nullable (only set for active impersonations)");

        columns.Should().ContainKey("impersonated_until",
            because: "AddImpersonationToUserSession migration must add impersonated_until");
        columns["impersonated_until"].DataType.Should().Be("timestamp with time zone",
            because: "impersonated_until must be timestamptz (consistent with other UTC datetimes in the schema)");
        columns["impersonated_until"].IsNullable.Should().Be("YES",
            because: "impersonated_until must be nullable (only set for active impersonations)");
    }

    [Fact]
    public async Task AddImpersonationToUserSession_FilteredIndexExists()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE tablename = 'user_sessions'
              AND indexname = 'ix_user_sessions_impersonated_by_user_id';
            """;

        var indexDef = (string?)await cmd.ExecuteScalarAsync(TestCancellationToken);
        indexDef.Should().NotBeNull(
            because: "the partial index ix_user_sessions_impersonated_by_user_id must be created");
        indexDef!.Should().Contain("impersonated_by_user_id IS NOT NULL",
            because: "the index must be PARTIAL — filtered on impersonated_by_user_id IS NOT NULL "
                   + "so it only contains the small set of active impersonations (GetActiveImpersonationsQuery, T6)");
    }
}
