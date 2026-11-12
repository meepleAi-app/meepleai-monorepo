using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Constants;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Commands;

/// <summary>
/// Due database veri, migrati: il «locale» e lo «staging». Creati una volta per classe (la migrazione costa
/// secondi); ogni test ripulisce solo la riga riservata, e le altre le semina con chiavi sue.
/// </summary>
public sealed class DatabaseSyncTwoDatabasesFixture(SharedTestcontainersFixture shared) : IAsyncLifetime
{
    private readonly string _localName = $"dbsync_local_{Guid.NewGuid():N}";
    private readonly string _remoteName = $"dbsync_remote_{Guid.NewGuid():N}";

    // Lo stesso utente in entrambi: system_configurations.CreatedByUserId ha una chiave esterna su users.
    public Guid AdminId { get; } = Guid.NewGuid();
    public string LocalConnectionString { get; private set; } = string.Empty;
    public string RemoteConnectionString { get; private set; } = string.Empty;

    public MeepleAiDbContext CreateContext(string connectionString) => shared.CreateDbContext(connectionString);

    public async ValueTask InitializeAsync()
    {
        LocalConnectionString = await shared.CreateIsolatedDatabaseAsync(_localName);
        RemoteConnectionString = await shared.CreateIsolatedDatabaseAsync(_remoteName);

        foreach (var connectionString in new[] { LocalConnectionString, RemoteConnectionString })
        {
            await using var db = CreateContext(connectionString);
            await db.Database.MigrateAsync();
            await TestSessionHelper.CreateAdminSessionAsync(db, AdminId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await shared.DropIsolatedDatabaseAsync(_localName);
        await shared.DropIsolatedDatabaseAsync(_remoteName);
    }
}

/// <summary>
/// #4994 (seguito di #4612): <c>DatabaseSync</c> copia le righe di <c>system_configurations</c> colonna per colonna, comprese
/// <c>BootstrapAdminCreated</c> e <c>BootstrapAdminCreatedAt</c>, e gli UPDATE vanno per chiave primaria. Una riga
/// riservata locale con lo stesso <c>Id</c> ma il flag azzerato, sincronizzata verso lo staging, avrebbe riaperto il
/// claim del primo admin sulla destinazione. La riga è per ambiente: portarla da un database all'altro, anche col
/// flag giusto, trasporta «qui il claim è stato consumato» dove non è vero. Il sync non la tocca, in nessuna
/// direzione.
/// </summary>
/// <remarks>
/// Il sync copia in una direzione per volta (<c>LocalToStaging</c> o <c>StagingToLocal</c>): inserisce le righe
/// solo della sorgente (<c>ON CONFLICT DO NOTHING</c>) e aggiorna per chiave primaria quelle differenti. Non
/// cancella mai: le righe solo della destinazione restano. Il diff si calcola su entrambi i lati con lo stesso
/// filtro, quindi la riga riservata della destinazione non risulta né differente né mancante.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DatabaseSync")]
[Trait("Issue", "4994")]
public sealed class SyncTableDataReservedRowIntegrationTests : IClassFixture<DatabaseSyncTwoDatabasesFixture>, IAsyncLifetime
{
    private const string Table = "system_configurations";
    private const string BootstrapKey = "Authentication:BootstrapAdmin";

    // Nel passato: una data riscritta con "adesso" si vede.
    private static readonly DateTime ClaimedAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    private readonly DatabaseSyncTwoDatabasesFixture _databases;

    public SyncTableDataReservedRowIntegrationTests(DatabaseSyncTwoDatabasesFixture databases)
    {
        _databases = databases;
    }

    public async ValueTask InitializeAsync()
    {
        foreach (var connectionString in new[] { _databases.LocalConnectionString, _databases.RemoteConnectionString })
        {
            await using var db = _databases.CreateContext(connectionString);
            // Anche le varianti della chiave seminate da FetchRowHashes_MatchesIsReservedOnEveryVariantOfTheKey.
            await db.Set<SystemConfigurationEntity>()
                .Where(c => c.Key.ToLower().Contains("bootstrapadmin"))
                .ExecuteDeleteAsync();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // SyncDirection è internal: un Theory pubblico non può prenderlo come parametro.
    public static TheoryData<bool> Directions => new() { true, false };

    private static SyncDirection DirectionOf(bool localToStaging) =>
        localToStaging ? SyncDirection.LocalToStaging : SyncDirection.StagingToLocal;

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Sync_WhenTheReservedRowDiffersByFlag_LeavesTheTargetRowAsItWas(bool localToStaging)
    {
        var direction = DirectionOf(localToStaging);
        var (source, target) = Sides(direction);
        var rowId = Guid.NewGuid();
        // Sorgente: la riga com'è dopo il difetto di #4612 (flag azzerato). Destinazione: claim consumato.
        await SeedAsync(source, ReservedRow(rowId, claimed: false, value: "false"));
        await SeedAsync(target, ReservedRow(rowId, claimed: true, value: "true"));
        // Controllo: lo stesso sync porta le altre righe, quindi non salta la tabella intera.
        var ordinaryKey = $"Test4612:Sync:{Guid.NewGuid():N}";
        await SeedAsync(source, OrdinaryRow(Guid.NewGuid(), ordinaryKey, "dalla-sorgente"));

        await RunSyncAsync(direction);

        var reserved = await ReadAsync(target, c => c.Id == rowId);
        reserved.Should().ContainSingle();
        reserved[0].BootstrapAdminCreated.Should().BeTrue("the sync must not reopen the first-admin claim on the target");
        reserved[0].BootstrapAdminCreatedAt.Should().Be(ClaimedAt);
        reserved[0].Value.Should().Be("true", "the whole reserved row stays as it was, not only the flag");
        (await ReadAsync(target, c => c.Key == ordinaryKey)).Should().ContainSingle(
            "control: the table is synced, only the reserved row is left out");
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Sync_WhenOnlyTheSourceHasTheReservedRow_DoesNotCreateItOnTheTarget(bool localToStaging)
    {
        var direction = DirectionOf(localToStaging);
        var (source, target) = Sides(direction);
        await SeedAsync(source, ReservedRow(Guid.NewGuid(), claimed: false, value: "true"));
        var ordinaryKey = $"Test4612:Sync:{Guid.NewGuid():N}";
        await SeedAsync(source, OrdinaryRow(Guid.NewGuid(), ordinaryKey, "dalla-sorgente"));

        await RunSyncAsync(direction);

        (await ReadAsync(target, c => c.Key == BootstrapKey)).Should().BeEmpty(
            "a row created by the sync would start with the claim open on an environment that never claimed it");
        (await ReadAsync(target, c => c.Key == ordinaryKey)).Should().ContainSingle("control: the other rows are synced");
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task Sync_WhenAnotherRowOfTheSourceHasThePrimaryKeyOfTheTargetReservedRow_DoesNotOverwriteIt(bool localToStaging)
    {
        var direction = DirectionOf(localToStaging);
        // UPDATE va per chiave primaria: una riga qualunque della sorgente con l'Id della riga riservata della
        // destinazione la sovrascriverebbe, chiave compresa.
        var (source, target) = Sides(direction);
        var rowId = Guid.NewGuid();
        await SeedAsync(target, ReservedRow(rowId, claimed: true, value: "true"));
        await SeedAsync(source, OrdinaryRow(rowId, $"Test4612:Sync:{Guid.NewGuid():N}", "intrusa"));

        await RunSyncAsync(direction);

        var reserved = await ReadAsync(target, c => c.Id == rowId);
        reserved.Should().ContainSingle();
        reserved[0].Key.Should().Be(BootstrapKey);
        reserved[0].BootstrapAdminCreated.Should().BeTrue();
        reserved[0].Value.Should().Be("true");
    }

    [Fact]
    public async Task FetchRowHashes_OfSystemConfigurations_LeavesOutTheReservedRow()
    {
        var reservedId = Guid.NewGuid();
        var ordinaryId = Guid.NewGuid();
        await SeedAsync(_databases.LocalConnectionString, ReservedRow(reservedId, claimed: true, value: "true"));
        await SeedAsync(_databases.LocalConnectionString, OrdinaryRow(ordinaryId, $"Test4612:Hash:{Guid.NewGuid():N}", "x"));

        await using var connection = new NpgsqlConnection(_databases.LocalConnectionString);
        await connection.OpenAsync();
        var pk = await DataDiffEngine.GetPrimaryKeyColumnsAsync(connection, "public", Table);
        var safe = DataDiffEngine.FilterSafeColumns(await DataDiffEngine.GetColumnsAsync(connection, "public", Table));
        var hashes = await DataDiffEngine.FetchRowHashesAsync(connection, "public", Table, pk, safe);

        hashes.Keys.Should().Contain(ordinaryId.ToString(), "control: ordinary rows are hashed");
        hashes.Keys.Should().NotContain(reservedId.ToString(), "the reserved row takes no part in a diff, so no sync can see it");
    }

    /// <summary>
    /// Il diff usa lo stesso criterio del resto del prodotto, <see cref="ReservedConfigurationKeys.IsReserved"/>.
    /// Le varianti con tab, a capo e spazio non separabile sono quelle che <c>btrim</c> di Postgres non toglie e
    /// <c>Trim</c> di C# sì: con un filtro SQL <c>lower(btrim(...))</c> restavano nel diff.
    /// </summary>
    [Theory]
    [InlineData("Authentication:BootstrapAdmin", true)]
    [InlineData("AUTHENTICATION:BOOTSTRAPADMIN", true)]
    [InlineData("  Authentication:BootstrapAdmin ", true)]
    [InlineData("\tAuthentication:BootstrapAdmin", true)]
    [InlineData("Authentication:BootstrapAdmin\n", true)]
    [InlineData("\u00A0Authentication:BootstrapAdmin\u00A0", true)]
    [InlineData("Authentication:BootstrapAdminToken", false)]
    [InlineData("Authentication:Bootstrap Admin", false)]
    public async Task FetchRowHashes_MatchesIsReservedOnEveryVariantOfTheKey(string key, bool reserved)
    {
        ReservedConfigurationKeys.IsReserved(key).Should().Be(reserved, "setup: the case states the criterion it checks");
        var rowId = Guid.NewGuid();
        var row = OrdinaryRow(rowId, key, "x");
        await SeedAsync(_databases.LocalConnectionString, row);

        await using var connection = new NpgsqlConnection(_databases.LocalConnectionString);
        await connection.OpenAsync();
        var pk = await DataDiffEngine.GetPrimaryKeyColumnsAsync(connection, "public", Table);
        var safe = DataDiffEngine.FilterSafeColumns(await DataDiffEngine.GetColumnsAsync(connection, "public", Table));
        var hashes = await DataDiffEngine.FetchRowHashesAsync(connection, "public", Table, pk, safe);

        hashes.ContainsKey(rowId.ToString()).Should().Be(
            !reserved, "the diff leaves out exactly the rows IsReserved recognises, and the sync writes only what the diff yields");
    }

    // ----- Aiuti -----

    private (string Source, string Target) Sides(SyncDirection direction) =>
        direction == SyncDirection.LocalToStaging
            ? (_databases.LocalConnectionString, _databases.RemoteConnectionString)
            : (_databases.RemoteConnectionString, _databases.LocalConnectionString);

    /// <summary>
    /// Esegue il sync. Il suo esito non si controlla qui: <c>SyncTableDataHandler</c> scrive l'audit dopo il commit
    /// con <c>Set&lt;AuditLog&gt;()</c>, un tipo di dominio che non è nel modello EF, quindi l'audit lancia
    /// «Cannot create a DbSet for 'AuditLog'» e il suo <c>catch</c> prova un rollback sulla transazione già
    /// chiusa. Il dato è già scritto, ed è quello che questi test guardano; la riga di controllo (le altre righe
    /// sincronizzate) prova che il commit c'è stato. Difetto preesistente, non di #4612: lo si accetta qui per nome
    /// e non si accetta altro.
    /// </summary>
    private async Task RunSyncAsync(SyncDirection direction)
    {
        var remote = new Mock<IRemoteDatabaseConnector>();
        remote
            .Setup(c => c.OpenConnectionAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                var connection = new NpgsqlConnection(_databases.RemoteConnectionString);
                await connection.OpenAsync(ct);
                return connection;
            });

        await using var local = _databases.CreateContext(_databases.LocalConnectionString);
        var handler = new SyncTableDataHandler(local, remote.Object, NullLogger<SyncTableDataHandler>.Instance);
        var target = direction == SyncDirection.LocalToStaging ? "STAGING" : "LOCAL";
        try
        {
            var result = await handler.Handle(
                new SyncTableDataCommand(Table, direction, $"SYNC {Table} TO {target}", _databases.AdminId),
                CancellationToken.None);
            result.Success.Should().BeTrue(result.ErrorMessage);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Transaction has completed", StringComparison.Ordinal))
        {
            // Vedi il commento del metodo.
        }
    }

    private SystemConfigurationEntity ReservedRow(Guid id, bool claimed, string value) => new()
    {
        Id = id,
        Key = BootstrapKey,
        Value = value,
        ValueType = "bool",
        Category = "Authentication",
        Environment = "All",
        IsActive = true,
        Version = 1,
        CreatedByUserId = _databases.AdminId,
        CreatedAt = ClaimedAt,
        UpdatedAt = ClaimedAt,
        BootstrapAdminCreated = claimed,
        BootstrapAdminCreatedAt = claimed ? ClaimedAt : null,
    };

    private SystemConfigurationEntity OrdinaryRow(Guid id, string key, string value) => new()
    {
        Id = id,
        Key = key,
        Value = value,
        ValueType = "string",
        Category = "Test4612",
        Environment = "All",
        IsActive = true,
        Version = 1,
        CreatedByUserId = _databases.AdminId,
        CreatedAt = ClaimedAt,
        UpdatedAt = ClaimedAt,
    };

    private async Task SeedAsync(string connectionString, SystemConfigurationEntity row)
    {
        await using var db = _databases.CreateContext(connectionString);
        db.Set<SystemConfigurationEntity>().Add(row);
        await db.SaveChangesAsync();
    }

    private async Task<List<SystemConfigurationEntity>> ReadAsync(
        string connectionString, System.Linq.Expressions.Expression<Func<SystemConfigurationEntity, bool>> predicate)
    {
        await using var db = _databases.CreateContext(connectionString);
        return await db.Set<SystemConfigurationEntity>().AsNoTracking().Where(predicate).ToListAsync();
    }
}
