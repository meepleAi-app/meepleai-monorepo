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
/// Integration test that verifies the ExtendAuditLogSchema migration applies cleanly
/// and produces the expected schema: 4 new columns on audit_logs + new audit_outbox table.
///
/// SP5 Admin Security S1 — Task 1 migration smoke test.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
public sealed class AuditSchemaMigrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AuditSchemaMigrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_audit_schema_{Guid.NewGuid():N}";
        _isolatedConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedConnectionString);
        var sp = services.BuildServiceProvider();
        _dbContext = sp.GetRequiredService<MeepleAiDbContext>();

        // Apply ALL migrations (including ExtendAuditLogSchema) against an isolated Postgres DB
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
    public async Task ExtendAuditLogSchema_AuditLogs_HasNewColumns()
    {
        // Query information_schema to verify the 4 new columns were added to audit_logs
        var connection = _dbContext!.Database.GetDbConnection();
        await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'audit_logs'
              AND column_name IN ('before_json', 'after_json', 'impersonated_user_id', 'step_up_token_id')
            ORDER BY column_name;
            """;

        var actualColumns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(TestCancellationToken);
        while (await reader.ReadAsync(TestCancellationToken))
        {
            actualColumns.Add(reader.GetString(0));
        }

        actualColumns.Should().BeEquivalentTo(
            ["after_json", "before_json", "impersonated_user_id", "step_up_token_id"],
            because: "ExtendAuditLogSchema migration must add all 4 new columns to audit_logs");
    }

    [Fact]
    public async Task ExtendAuditLogSchema_AuditLogs_NewJsonColumnsAreJsonb()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type, udt_name
            FROM information_schema.columns
            WHERE table_name = 'audit_logs'
              AND column_name IN ('before_json', 'after_json')
            ORDER BY column_name;
            """;

        var jsonColumns = new Dictionary<string, string>();
        await using var reader = await cmd.ExecuteReaderAsync(TestCancellationToken);
        while (await reader.ReadAsync(TestCancellationToken))
        {
            jsonColumns[reader.GetString(0)] = reader.GetString(2); // udt_name = 'jsonb'
        }

        jsonColumns.Should().ContainKey("after_json").WhoseValue.Should().Be("jsonb",
            because: "after_json must be stored as Postgres jsonb type");
        jsonColumns.Should().ContainKey("before_json").WhoseValue.Should().Be("jsonb",
            because: "before_json must be stored as Postgres jsonb type");
    }

    [Fact]
    public async Task ExtendAuditLogSchema_AuditOutbox_TableExists()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_name = 'audit_outbox';
            """;

        var count = (long)(await cmd.ExecuteScalarAsync(TestCancellationToken))!;
        count.Should().Be(1, because: "audit_outbox table must be created by the migration");
    }

    [Fact]
    public async Task ExtendAuditLogSchema_AuditOutbox_HasExpectedColumns()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'audit_outbox'
            ORDER BY column_name;
            """;

        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(TestCancellationToken);
        while (await reader.ReadAsync(TestCancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        columns.Should().BeEquivalentTo(
            ["id", "created_at", "last_error", "payload_json", "processed_at", "retry_count", "status"],
            because: "audit_outbox must have the expected columns from AuditOutboxEntityConfiguration");
    }

    [Fact]
    public async Task ExtendAuditLogSchema_AuditOutbox_PayloadJsonIsJsonb()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT udt_name
            FROM information_schema.columns
            WHERE table_name = 'audit_outbox'
              AND column_name = 'payload_json';
            """;

        var udtName = (string)(await cmd.ExecuteScalarAsync(TestCancellationToken))!;
        udtName.Should().Be("jsonb", because: "payload_json must be stored as Postgres jsonb type");
    }

    [Fact]
    public async Task ExtendAuditLogSchema_ImpersonatedUserIdIndex_Exists()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE tablename = 'audit_logs'
              AND indexname = 'ix_audit_logs_impersonated_user_id';
            """;

        var count = (long)(await cmd.ExecuteScalarAsync(TestCancellationToken))!;
        count.Should().Be(1, because: "partial index ix_audit_logs_impersonated_user_id must be created");
    }

    [Fact]
    public async Task ExtendAuditLogSchema_AuditOutboxStatusIndex_Exists()
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(TestCancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE tablename = 'audit_outbox'
              AND indexname = 'ix_audit_outbox_status_created_at';
            """;

        var count = (long)(await cmd.ExecuteScalarAsync(TestCancellationToken))!;
        count.Should().Be(1, because: "composite index ix_audit_outbox_status_created_at must be created on audit_outbox");
    }
}
