using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Commands;

/// <summary>
/// #5002 — l'audit del sync e dell'applicazione delle migrazioni.
/// </summary>
/// <remarks>
/// <para>
/// I due gestori scrivevano l'audit con <c>Set&lt;AuditLog&gt;()</c>, l'aggregato di dominio di Administration, che
/// il modello EF non mappa: «Cannot create a DbSet for 'AuditLog'». La scrittura veniva dopo <c>CommitAsync</c> nello
/// stesso <c>try</c>, e il <c>catch</c> chiamava <c>RollbackAsync</c> su una transazione già chiusa. I dati erano
/// scritti, l'admin riceveva un'eccezione al posto di <c>SyncResult(true)</c>, e la riga di audit mancava.
/// </para>
/// <para>
/// L'audit si scrive sempre nel database <b>locale</b> (il contesto del gestore), qualunque sia la direzione; nel
/// verso <c>StagingToLocal</c> la transazione dei dati è sulla stessa connessione del contesto.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DatabaseSync")]
[Trait("Issue", "5002")]
public sealed class DatabaseSyncAuditIntegrationTests : IClassFixture<DatabaseSyncTwoDatabasesFixture>
{
    private const string Table = "system_configurations";

    private readonly DatabaseSyncTwoDatabasesFixture _databases;
    private readonly SharedTestcontainersFixture _shared;

    public DatabaseSyncAuditIntegrationTests(DatabaseSyncTwoDatabasesFixture databases, SharedTestcontainersFixture shared)
    {
        _databases = databases;
        _shared = shared;
    }

    // SyncDirection è internal: un Theory pubblico non può prenderlo come parametro.
    public static TheoryData<bool> Directions => new() { true, false };

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task SyncTableData_WithRowsToWrite_ReturnsSuccess_AndWritesItsAuditRow(bool localToStaging)
    {
        var direction = localToStaging ? SyncDirection.LocalToStaging : SyncDirection.StagingToLocal;
        var (source, target) = localToStaging
            ? (_databases.LocalConnectionString, _databases.RemoteConnectionString)
            : (_databases.RemoteConnectionString, _databases.LocalConnectionString);
        var key = $"Test5002:Sync:{Guid.NewGuid():N}";
        await SeedConfigurationAsync(source, key, _databases.AdminId);

        var result = await SyncAsync(direction);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Inserted.Should().BeGreaterThanOrEqualTo(1);
        (await ConfigurationRowsAsync(target, key)).Should().Be(1, "control: the row was written on the target");
        var audit = await AuditRowAsync("DatabaseSync.SyncTableData", result.OperationId);
        audit.Result.Should().Be("Success");
        audit.UserId.Should().Be(_databases.AdminId);
        audit.Resource.Should().Be(Table);
    }

    [Fact]
    public async Task SyncTableData_WhenTheTargetRefusesARow_ReturnsFailure_RollsBack_AndWritesTheFailureAudit()
    {
        // The author of the row exists only in the local database: on the target the foreign key refuses the insert,
        // inside the transaction. Before #5002 the failure audit threw too, so the caller got an exception.
        var localOnlyAuthor = Guid.NewGuid();
        await using (var local = _databases.CreateContext(_databases.LocalConnectionString))
        {
            await TestSessionHelper.CreateUserSessionAsync(local, localOnlyAuthor);
        }

        var key = $"Test5002:Refused:{Guid.NewGuid():N}";
        await SeedConfigurationAsync(_databases.LocalConnectionString, key, localOnlyAuthor);
        try
        {
            var result = await SyncAsync(SyncDirection.LocalToStaging);

            result.Success.Should().BeFalse();
            (await ConfigurationRowsAsync(_databases.RemoteConnectionString, key)).Should().Be(0, "the transaction was rolled back");
            (await AuditRowAsync("DatabaseSync.SyncTableData", result.OperationId)).Result.Should().Be("Failure");
        }
        finally
        {
            // The sync copies the whole table: left behind, this row would make every later sync of the class fail.
            await using var local = _databases.CreateContext(_databases.LocalConnectionString);
            await local.Set<SystemConfigurationEntity>().Where(c => c.Key == key).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task ApplyMigrations_ToAStagingOneMigrationBehind_ReturnsSuccess_AndWritesItsAuditRow()
    {
        var localName = $"dbsync_mig_local_{Guid.NewGuid():N}";
        var remoteName = $"dbsync_mig_remote_{Guid.NewGuid():N}";
        var localConnection = await _shared.CreateIsolatedDatabaseAsync(localName);
        var remoteConnection = await _shared.CreateIsolatedDatabaseAsync(remoteName);
        try
        {
            var adminId = Guid.NewGuid();
            string lastMigration;
            await using (var local = _shared.CreateDbContext(localConnection))
            {
                await local.Database.MigrateAsync();
                await TestSessionHelper.CreateAdminSessionAsync(local, adminId);
                lastMigration = local.Database.GetMigrations().Last();
            }

            await using (var remote = _shared.CreateDbContext(remoteConnection))
            {
                var oneBehind = remote.Database.GetMigrations().SkipLast(1).Last();
                await remote.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(oneBehind);
            }

            await using var handlerContext = _shared.CreateDbContext(localConnection);
            var handler = new ApplyMigrationsHandler(
                handlerContext, RemoteConnector(remoteConnection), NullLogger<ApplyMigrationsHandler>.Instance);

            var result = await handler.Handle(
                new ApplyMigrationsCommand(SyncDirection.LocalToStaging, "APPLY 1 MIGRATIONS TO STAGING", adminId),
                CancellationToken.None);

            result.Success.Should().BeTrue(result.ErrorMessage);
            result.Inserted.Should().Be(1);
            await using (var remote = _shared.CreateDbContext(remoteConnection))
            {
                (await remote.Database.GetAppliedMigrationsAsync()).Should().Contain(lastMigration, "control: the migration was applied");
            }

            await using var reader = _shared.CreateDbContext(localConnection);
            var audit = await reader.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.Action == "DatabaseSync.ApplyMigrations" && a.ResourceId == result.OperationId.ToString());
            audit.Result.Should().Be("Success");
            audit.UserId.Should().Be(adminId);
        }
        finally
        {
            await _shared.DropIsolatedDatabaseAsync(localName);
            await _shared.DropIsolatedDatabaseAsync(remoteName);
        }
    }

    [Fact]
    public async Task ApplyMigrations_WhenTheLastMigrationFails_RollsBackTheOnesBefore_AndWritesTheFailureAudit()
    {
        // The staging is two migrations behind, and a trigger refuses to record the last one: the script fails at its
        // very end, after the migration before has run. Without NoTransactions (#5002) EF's own COMMIT after each
        // migration would leave that one applied; in a single transaction nothing stays.
        var localName = $"dbsync_mig_local_{Guid.NewGuid():N}";
        var remoteName = $"dbsync_mig_remote_{Guid.NewGuid():N}";
        var localConnection = await _shared.CreateIsolatedDatabaseAsync(localName);
        var remoteConnection = await _shared.CreateIsolatedDatabaseAsync(remoteName);
        try
        {
            var adminId = Guid.NewGuid();
            await using (var local = _shared.CreateDbContext(localConnection))
            {
                await local.Database.MigrateAsync();
                await TestSessionHelper.CreateAdminSessionAsync(local, adminId);
            }

            List<string> historyBefore;
            await using (var remote = _shared.CreateDbContext(remoteConnection))
            {
                var migrations = remote.Database.GetMigrations().ToList();
                await remote.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync(migrations[^3]);
                await remote.Database.ExecuteSqlRawAsync(
                    "CREATE FUNCTION refuse_last_migration_5002() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN " +
                    "IF NEW.\"MigrationId\" = '" + migrations[^1] + "' THEN RAISE EXCEPTION 'refused by the test of #5002'; END IF; " +
                    "RETURN NEW; END $$; " +
                    "CREATE TRIGGER refuse_last_migration_5002 BEFORE INSERT ON \"__EFMigrationsHistory\" " +
                    "FOR EACH ROW EXECUTE FUNCTION refuse_last_migration_5002();");
                historyBefore = (await remote.Database.GetAppliedMigrationsAsync()).ToList();
            }

            await using var handlerContext = _shared.CreateDbContext(localConnection);
            var handler = new ApplyMigrationsHandler(
                handlerContext, RemoteConnector(remoteConnection), NullLogger<ApplyMigrationsHandler>.Instance);

            var result = await handler.Handle(
                new ApplyMigrationsCommand(SyncDirection.LocalToStaging, "APPLY 2 MIGRATIONS TO STAGING", adminId),
                CancellationToken.None);

            result.Success.Should().BeFalse("setup: the trigger refuses the last migration");
            result.ErrorMessage.Should().Contain("refused by the test of #5002", "setup: the failure is the one of the trigger");
            await using (var remote = _shared.CreateDbContext(remoteConnection))
            {
                (await remote.Database.GetAppliedMigrationsAsync()).Should().Equal(historyBefore,
                    "the apply is all or nothing: no migration of the script stays applied");
            }

            await using var reader = _shared.CreateDbContext(localConnection);
            var audit = await reader.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.Action == "DatabaseSync.ApplyMigrations" && a.ResourceId == result.OperationId.ToString());
            audit.Result.Should().Be("Failure");
        }
        finally
        {
            await _shared.DropIsolatedDatabaseAsync(localName);
            await _shared.DropIsolatedDatabaseAsync(remoteName);
        }
    }

    // ----- Aiuti -----

    private async Task<SyncResult> SyncAsync(SyncDirection direction)
    {
        await using var local = _databases.CreateContext(_databases.LocalConnectionString);
        var handler = new SyncTableDataHandler(
            local, RemoteConnector(_databases.RemoteConnectionString), NullLogger<SyncTableDataHandler>.Instance);
        var target = direction == SyncDirection.LocalToStaging ? "STAGING" : "LOCAL";
        return await handler.Handle(
            new SyncTableDataCommand(Table, direction, $"SYNC {Table} TO {target}", _databases.AdminId),
            CancellationToken.None);
    }

    private static IRemoteDatabaseConnector RemoteConnector(string connectionString)
    {
        var remote = new Mock<IRemoteDatabaseConnector>();
        remote
            .Setup(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(ct);
                return connection;
            });
        return remote.Object;
    }

    private async Task SeedConfigurationAsync(string connectionString, string key, Guid author)
    {
        await using var db = _databases.CreateContext(connectionString);
        var now = DateTime.UtcNow;
        db.Set<SystemConfigurationEntity>().Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = "x",
            ValueType = "string",
            Category = "Test",
            Environment = "All",
            IsActive = true,
            Version = 1,
            CreatedByUserId = author,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> ConfigurationRowsAsync(string connectionString, string key)
    {
        await using var db = _databases.CreateContext(connectionString);
        return await db.Set<SystemConfigurationEntity>().CountAsync(c => c.Key == key);
    }

    private async Task<AuditLogEntity> AuditRowAsync(string action, Guid operationId)
    {
        await using var db = _databases.CreateContext(_databases.LocalConnectionString);
        return await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == action && a.ResourceId == operationId.ToString());
    }
}
