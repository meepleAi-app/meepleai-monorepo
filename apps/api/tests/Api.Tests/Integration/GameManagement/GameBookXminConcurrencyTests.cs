using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Concorrenza ottimistica su <c>game_books</c> (#3651, lotto 3).
///
/// Come le altre entità del censimento, <c>GameBook</c> dichiarava la protezione con
/// <c>byte[]? RowVersion</c> su una colonna <c>bytea</c> che nulla popola da quando #2305 ha
/// rimosso il trigger: il token restava NULL, EF confrontava <c>NULL = NULL</c> e ogni update
/// passava.
///
/// I test coprono il percorso reale — <see cref="GameBookRepository"/> — e non il DbContext, ed è
/// una lezione pagata nel lotto 2: lì i test sul solo DbContext erano verdi mentre il repository
/// era rotto. <c>UpdateAsync</c> riattacca un'istanza detached con <c>DbSet.Update()</c>, e con un
/// token di concorrenza quel percorso ha due modi di sbagliare opposti fra loro. Servono quindi
/// entrambe le direzioni:
///
/// <list type="number">
/// <item>uno scrittore solo deve <b>riuscire</b> — un token che rifiuta ogni scrittura non
/// protegge nulla, rompe soltanto (è il guasto trovato su <c>SharedGame</c>: senza il valore
/// letto dal DB l'UPDATE emette <c>WHERE xmin = 0</c>, che non corrisponde mai);</item>
/// <item>due scrittori concorrenti: il secondo deve essere <b>rifiutato</b>.</item>
/// </list>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameBookXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameBookXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamebook_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static GameBook NewBook() => GameBook.CreateCommunity(
        GameRef.Shared(Guid.NewGuid()),
        "Regolamento base",
        GameBookRole.RulesReference,
        ParagraphScheme.None,
        "it",
        sequentialRead: false,
        kbSourceDocId: null,
        physicalOnly: false,
        createdBy: Guid.NewGuid());

    [Fact(DisplayName = "GameBook: un solo scrittore riesce — il token non blocca chi non ha rivali")]
    public async Task Update_WithNoConcurrentWriter_Succeeds()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var repository = new GameBookRepository(_dbContext);
        var book = NewBook();
        await repository.AddAsync(book, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // ── Act: rileggo, rinomino e persisto, senza altri scrittori ──────────────
        var loaded = await repository.GetByIdAsync(book.Id, CancellationToken.None);
        loaded.Should().NotBeNull();

        loaded!.Rename("Regolamento base — 2ª edizione", Guid.NewGuid());
        await repository.UpdateAsync(loaded, CancellationToken.None);

        Func<Task> act = async () => await _dbContext.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().NotThrowAsync(
            "senza scritture concorrenti l'update deve passare: se il token letto dal DB non " +
            "arriva all'UPDATE, la clausola diventa `WHERE xmin = 0` e nessuna riga corrisponde");

        _dbContext.ChangeTracker.Clear();
        var reloaded = await repository.GetByIdAsync(book.Id, CancellationToken.None);
        reloaded!.DisplayName.Should().Be("Regolamento base — 2ª edizione");
    }

    [Fact(DisplayName = "GameBook: due editor concorrenti, il secondo è rifiutato")]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var seedRepository = new GameBookRepository(_dbContext);
        var book = NewBook();
        await seedRepository.AddAsync(book, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);
        var repoA = new GameBookRepository(dbA);
        var repoB = new GameBookRepository(dbB);

        var bookA = await repoA.GetByIdAsync(book.Id, CancellationToken.None);
        var bookB = await repoB.GetByIdAsync(book.Id, CancellationToken.None);
        bookA.Should().NotBeSameAs(bookB, "sono due scope indipendenti sulla stessa riga");

        // ── Act: A committa per primo, B resta con un token vecchio ───────────────
        bookA!.Rename("Rinominato da A", Guid.NewGuid());
        await repoA.UpdateAsync(bookA, CancellationToken.None);
        await dbA.SaveChangesAsync();

        bookB!.Rename("Rinominato da B", Guid.NewGuid());
        await repoB.UpdateAsync(bookB, CancellationToken.None);
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "B ha letto prima del commit di A: la sua scrittura va rifiutata invece di " +
            "sovrascrivere in silenzio la rinomina del primo editor");
    }
}
