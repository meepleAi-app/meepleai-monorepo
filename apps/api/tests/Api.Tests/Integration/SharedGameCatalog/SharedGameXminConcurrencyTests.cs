using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Prova che <c>shared_games</c> rilevi davvero un conflitto di scrittura concorrente.
///
/// Prima di #3651 la tabella dichiarava la concorrenza ottimistica con <c>byte[]? RowVersion</c>
/// su una colonna <c>bytea</c>. Postgres non popola una <c>bytea</c> da solo, e il trigger che lo
/// faceva è stato rimosso da #2305 nel passaggio a <c>xmin</c> delle altre entità: da allora il
/// token restava NULL su ogni riga, EF confrontava <c>NULL = NULL</c> a ogni update, e nessun
/// conflitto veniva mai rilevato. La protezione era dichiarata ma inesistente.
///
/// Lo scenario coperto è l'editing concorrente dal catalogo: due redattori aprono la stessa
/// scheda gioco e salvano modifiche a campi diversi. Senza token la seconda scrittura sovrascrive
/// la prima in silenzio (last-write-wins), e chi ha salvato per primo non ha modo di accorgersene.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public SharedGameXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"sharedgame_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Editing concorrente della stessa scheda: la seconda scrittura è rifiutata")]
    public async Task ConcurrentEdits_SecondWriterThrowsConcurrencyException()
    {
        // ── Arrange: una scheda pubblicata nel catalogo ───────────────────────────
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Gloomhaven"
        });
        await _dbContext.SaveChangesAsync();

        // ── Due scope indipendenti aprono la stessa scheda ─────────────────────────
        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);

        var gameA = await dbA.SharedGames.FirstAsync(g => g.Id == gameId);
        var gameB = await dbB.SharedGames.FirstAsync(g => g.Id == gameId);

        gameA.Should().NotBeSameAs(gameB);

        // ── Act: A salva per primo ────────────────────────────────────────────────
        gameA.Title = "Gloomhaven: Jaws of the Lion";
        await dbA.SaveChangesAsync();

        // B modifica un campo diverso, quindi non c'è collisione a livello di colonna: senza un
        // token di concorrenza il suo UPDATE riesce e riporta il Title al valore che A aveva
        // appena cambiato. È il caso in cui il last-write-wins è più insidioso — non c'è alcun
        // sintomo, solo una modifica che sparisce.
        gameB.Description = "Un dungeon crawler cooperativo a campagna.";
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "lo scope B ha un token stale dopo il commit di A — la seconda scrittura va rifiutata " +
            "invece di sovrascrivere silenziosamente la modifica del primo redattore");
    }
}
