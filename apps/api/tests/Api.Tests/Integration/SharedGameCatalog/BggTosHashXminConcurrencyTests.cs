using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Concorrenza ottimistica su <c>bgg_tos_hashes</c> (#3651, lotto 4).
///
/// <para>
/// La riga è un <b>singleton</b> (<see cref="BggTosHashEntity.SingletonId"/>) aggiornato da
/// <c>BggTosWatcherJob</c>. La race non è teorica: il job è schedulato, e un dispatch manuale o una
/// seconda istanza dell'API durante un deploy leggono e riscrivono la stessa riga. Senza un token
/// attivo l'ultima scrittura vince in silenzio, e ciò che si perde è <c>ChangeCount</c> — il
/// contatore che dice quante volte i termini di BGG sono cambiati, cioè il dato su cui poggia la
/// revisione legale di spec §8.5.6.
/// </para>
/// <para>
/// <b>Perché questo test viene prima della conversione.</b> Con il token <c>byte[]</c> su colonna
/// <c>bytea</c>, Postgres non lo popola mai: EF confronta <c>NULL = NULL</c> a ogni update e nessun
/// conflitto viene rilevato. Il test deve quindi fallire con «no exception was thrown» — non con un
/// errore di setup — e passare dopo il passaggio a <c>uint Xmin</c> sulla colonna di sistema.
/// </para>
/// <para>
/// Il test passa dal <see cref="MeepleAiDbContext"/> e non da un repository perché questa entità non
/// ne ha uno: <c>BggTosWatcherJob:129-169</c> carica la riga tracked, la muta e salva. È la tecnica
/// <c>TrackedMutation</c>, quella che conserva l'original value caricato, quindi la conversione non
/// espone al difetto opposto (#3688).
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3651")]
public sealed class BggTosHashXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public BggTosHashXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"bggtos_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task SeedSingletonAsync()
    {
        _dbContext.BggTosHashes.Add(new BggTosHashEntity
        {
            Id = BggTosHashEntity.SingletonId,
            CurrentHash = new string('a', 64),
            LastCheckedAt = DateTime.UtcNow,
            LastChangedAt = null,
            ChangeCount = 0,
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        await SeedSingletonAsync();

        // Due contesti distinti leggono la stessa riga: è la forma minima della race che il job
        // subisce quando un dispatch manuale si sovrappone al tick schedulato.
        await using var first = _fixture.CreateDbContext(_connectionString);
        await using var second = _fixture.CreateDbContext(_connectionString);

        // Issue #3866: `.AsTracking()` is REQUIRED here. The DbContext default is NoTracking
        // (PERF-06), so a plain read hands back a DETACHED entity: the mutations below would reach
        // no change tracker, SaveChangesAsync would write nothing, and the concurrency token this
        // test exists to exercise would never even be compared. This is the documented opt-out for
        // a fixture whose subject IS a tracked read-modify-write.
        var rowSeenByFirst = await first.BggTosHashes
            .AsTracking()
            .FirstAsync(x => x.Id == BggTosHashEntity.SingletonId);
        var rowSeenBySecond = await second.BggTosHashes
            .AsTracking()
            .FirstAsync(x => x.Id == BggTosHashEntity.SingletonId);

        rowSeenByFirst.CurrentHash = new string('b', 64);
        rowSeenByFirst.ChangeCount++;
        rowSeenByFirst.LastChangedAt = DateTime.UtcNow;
        await first.SaveChangesAsync();

        // Il secondo scrittore lavora su una versione ormai superata. Con il token attivo Postgres
        // non trova la tupla che si aspetta e l'update non tocca alcuna riga.
        rowSeenBySecond.CurrentHash = new string('c', 64);
        rowSeenBySecond.ChangeCount++;

        var secondWrite = async () => await second.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "la seconda scrittura parte da una riga già superata: senza conflitto rilevato il "
            + "ChangeCount della prima andrebbe perso in silenzio (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        await SeedSingletonAsync();

        // L'altra direzione: il token non deve rompere la scrittura normale, che è il modo in cui
        // una conversione a xmin fallisce quando il write-path non porta il token (#3688).
        await using var context = _fixture.CreateDbContext(_connectionString);

        var row = await context.BggTosHashes.AsTracking().FirstAsync(x => x.Id == BggTosHashEntity.SingletonId); // #3866
        row.CurrentHash = new string('d', 64);
        row.LastCheckedAt = DateTime.UtcNow;

        var write = async () => await context.SaveChangesAsync();

        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare: se anche questa fallisse, il token sarebbe "
            + "acceso ma il write-path non lo preserverebbe");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.BggTosHashes
            .FirstAsync(x => x.Id == BggTosHashEntity.SingletonId);
        persisted.CurrentHash.Should().Be(new string('d', 64));
    }
}
