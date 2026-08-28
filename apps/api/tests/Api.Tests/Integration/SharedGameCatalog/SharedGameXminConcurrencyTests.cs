using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
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

        // Issue #3866: `.AsTracking()` is REQUIRED here. The DbContext default is NoTracking
        // (PERF-06), so a plain read hands back a DETACHED entity: the mutations below would reach
        // no change tracker, SaveChangesAsync would write nothing, and the concurrency token this
        // test exists to exercise would never even be compared. This is the documented opt-out for
        // a fixture whose subject IS a tracked read-modify-write.
        var gameA = await dbA.SharedGames.AsTracking().FirstAsync(g => g.Id == gameId);
        var gameB = await dbB.SharedGames.AsTracking().FirstAsync(g => g.Id == gameId);

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

    // ── Il percorso reale: SharedGameRepository.Update() ──────────────────────────
    //
    // I due test qui sotto coprono il repository invece del DbContext, perché è lì che passano i
    // ~30 handler che scrivono sul catalogo — e perché `Update()` persiste un grafo **detached**
    // (`MapToEntity` + `DbSet.Update()`), che con un token di concorrenza si comporta in modo
    // diverso da una riga tracciata: EF non ha un original value da cui partire e usa quello che
    // trova sulla proprietà.
    //
    // Senza il trasporto di `XminVersion` attraverso l'aggregato, quel valore è `0` — mai un xid
    // reale — quindi ogni UPDATE emette `WHERE xmin = 0`, colpisce 0 righe e solleva
    // `DbUpdateConcurrencyException` **anche senza concorrenza**: il rovescio esatto del difetto
    // che #3651 corregge. Il primo test è la regressione di quel guasto, il secondo verifica che
    // la protezione resti attiva sullo stesso percorso.

    private SharedGameRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    private static SharedGame NewGame() => SharedGame.Create(
        "Ticket to Ride", 2004, "Descrizione originale", 2, 5, 60, 8,
        null, null, "https://example.com/c.jpg", "https://example.com/t.jpg", null, Guid.NewGuid());

    [Fact(DisplayName = "Update() da singolo scrittore riesce: il token non blocca chi non ha rivali")]
    public async Task RepositoryUpdate_WithNoConcurrentWriter_Succeeds()
    {
        // ── Arrange: una scheda salvata, poi il tracker svuotato per simulare una request nuova ──
        var repository = CreateRepository(_dbContext);
        var game = NewGame();
        await repository.AddAsync(game);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // ── Act: rileggo, modifico e persisto — nessun altro scrittore in gioco ───
        var loaded = await repository.GetByIdAsync(game.Id);
        loaded.Should().NotBeNull();
        loaded!.XminVersion.Should().NotBe(0,
            "l'aggregato deve trasportare il token letto dal DB: se resta 0 l'UPDATE cercherebbe " +
            "`WHERE xmin = 0`, che non corrisponde ad alcuna riga");

        loaded.UpdateInfo(
            title: "Ticket to Ride",
            yearPublished: 2004,
            description: "Descrizione aggiornata",
            minPlayers: 2,
            maxPlayers: 5,
            playingTimeMinutes: 60,
            minAge: 8,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/c.jpg",
            thumbnailUrl: "https://example.com/t.jpg",
            rules: null,
            modifiedBy: Guid.NewGuid());
        repository.Update(loaded);

        Func<Task> act = async () => await _dbContext.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().NotThrowAsync(
            "senza scritture concorrenti l'update deve passare: un token di concorrenza che " +
            "rifiuta ogni scrittura non protegge nulla, rompe soltanto");

        _dbContext.ChangeTracker.Clear();
        var reloaded = await repository.GetByIdAsync(game.Id);
        reloaded!.Description.Should().Be("Descrizione aggiornata");
    }

    [Fact(DisplayName = "Update() con token stale è rifiutato: la protezione vale anche sul repository")]
    public async Task RepositoryUpdate_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var seedRepository = CreateRepository(_dbContext);
        var game = NewGame();
        await seedRepository.AddAsync(game);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);
        var repoA = CreateRepository(dbA);
        var repoB = CreateRepository(dbB);

        var gameA = await repoA.GetByIdAsync(game.Id);
        var gameB = await repoB.GetByIdAsync(game.Id);
        gameA!.XminVersion.Should().Be(gameB!.XminVersion, "entrambi hanno letto la stessa riga");

        // ── Act: A committa per primo, B resta con un token vecchio ───────────────
        gameA.UpdateInfo("Ticket to Ride", 2004, "Modificata da A", 2, 5, 60, 8,
            null, null, "https://example.com/c.jpg", "https://example.com/t.jpg", null, Guid.NewGuid());
        repoA.Update(gameA);
        await dbA.SaveChangesAsync();

        gameB.UpdateInfo("Ticket to Ride", 2004, "Modificata da B", 2, 5, 60, 8,
            null, null, "https://example.com/c.jpg", "https://example.com/t.jpg", null, Guid.NewGuid());
        repoB.Update(gameB);
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "il token trasportato da B è quello di prima del commit di A: la seconda scrittura " +
            "va rifiutata anche passando dal repository, non solo dal DbContext");
    }
}
