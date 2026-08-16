using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Concorrenza ottimistica su <c>catalog_seed_drafts</c> (#3651, lotto 5).
///
/// <para>
/// Il dominio prevede la race: una draft attraversa <c>Pending → Fetched → Approved</c> mossa da
/// un job di arricchimento e da un'azione di amministrazione che possono sovrapporsi. Senza token
/// attivo l'ultima scrittura vince in silenzio, e ciò che si perde è lo <b>stato</b>: una draft
/// approvata può tornare Pending, o un <c>ResultingSharedGameId</c> appena assegnato sparire.
/// </para>
/// <para>
/// <b>Perché il test viene prima della conversione.</b> Con il token <c>byte[]</c> su colonna
/// <c>bytea</c> Postgres non lo popola mai: EF confronta <c>NULL = NULL</c> e nessun conflitto
/// viene rilevato. Il test deve fallire con «no exception was thrown», non con un errore di setup.
/// </para>
/// <para>
/// Write-path: <c>CatalogSeedDraftRepository</c> legge <c>AsTracking()</c> sui percorsi di
/// scrittura (commento in testa alla classe, PERF-06) e muta in place — tecnica
/// <c>TrackedMutation</c>, che conserva l'original value caricato. La conversione non espone
/// quindi al difetto opposto (#3688).
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3651")]
public sealed class CatalogSeedDraftXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public CatalogSeedDraftXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"seeddraft_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<Guid> SeedDraftAsync()
    {
        var id = Guid.NewGuid();
        _dbContext.CatalogSeedDrafts.Add(new CatalogSeedDraftEntity
        {
            Id = id,
            BggId = 13,
            Status = "Pending",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedDraftAsync();

        await using var first = _fixture.CreateDbContext(_connectionString);
        await using var second = _fixture.CreateDbContext(_connectionString);

        var seenByFirst = await first.CatalogSeedDrafts.FirstAsync(d => d.Id == id);
        var seenBySecond = await second.CatalogSeedDrafts.FirstAsync(d => d.Id == id);

        seenByFirst.Status = "Fetched";
        seenByFirst.FetchedAt = DateTime.UtcNow;
        await first.SaveChangesAsync();

        // Il secondo scrittore parte da una riga superata: approva una draft che nel frattempo
        // è stata rifetchata. Senza conflitto rilevato, l'approvazione sovrascrive.
        seenBySecond.Status = "Approved";
        seenBySecond.ApprovedAt = DateTime.UtcNow;

        var secondWrite = async () => await second.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due transizioni di stato concorrenti sulla stessa draft devono escludersi: senza "
            + "conflitto rilevato l'ultima vince e lo stato intermedio sparisce (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedDraftAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var draft = await context.CatalogSeedDrafts.FirstAsync(d => d.Id == id);
        draft.Status = "Fetched";

        var write = async () => await context.SaveChangesAsync();

        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare: se anche questa fallisse, il token sarebbe "
            + "acceso ma il write-path non lo preserverebbe (#3688)");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        (await verification.CatalogSeedDrafts.FirstAsync(d => d.Id == id)).Status.Should().Be("Fetched");
    }
}
