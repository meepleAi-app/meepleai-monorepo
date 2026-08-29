using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Concorrenza ottimistica su <c>user_library_entries</c> (#3651, lotto 6).
///
/// <para>
/// La race è quotidiana: la stessa voce di libreria viene toccata da due schede aperte, o da
/// un'azione dell'utente mentre un job di manutenzione la aggiorna. Senza token attivo l'ultima
/// scrittura vince e ciò che si perde sono le note o lo stato di gioco dell'utente.
/// </para>
/// <para>
/// <b>Perché questa entità è più delicata delle altre del lotto.</b>
/// <c>UserLibraryRepository.UpdateAsync</c> (<c>:316-324</c>) fa <c>MapToPersistence(entry)</c> +
/// <c>DbSet.Update(entity)</c> su un grafo <b>detached</b>, e il mapper costruisce un'entità nuova.
/// È esattamente la combinazione di #3688: accendere il token senza farlo attraversare il mapper
/// trasformerebbe il guasto di #3651 («non protegge nulla») in quello di #3688 («rifiuta ogni
/// scrittura»), perché la WHERE conterrebbe <c>xmin = 0</c>.
/// </para>
/// <para>
/// Per questo il secondo test non è simmetria di cortesia: <see
/// cref="Update_WithoutConcurrentWrite_Succeeds"/> è il test che fallisce se la conversione è
/// fatta a metà, e va letto come parte del contratto quanto il primo.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "3651")]
public sealed class UserLibraryEntryXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public UserLibraryEntryXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"userlib_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static UserLibraryRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object, NullLogger<UserLibraryRepository>.Instance);

    /// <summary>
    /// Semina la riga direttamente: il percorso di scrittura del repository è ciò che i test
    /// devono esercitare, non ciò da cui dipendono per arrivare allo stato iniziale.
    /// </summary>
    private async Task<Guid> SeedEntryAsync()
    {
        // Due FK, entrambe da soddisfare prima del token: `GameId` è mappato su `shared_game_id`
        // (→ shared_games) e `UserId` va a `users`. Con Guid inventati l'INSERT fallisce con 23503
        // prima ancora di arrivare al concurrency token, e il test rosso direbbe la cosa sbagliata
        // (pitfall #2620).
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Gioco in libreria",
        });

        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = $"libreria-{userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });

        var id = Guid.NewGuid();
        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = id,
            UserId = userId,
            GameId = gameId,
            Notes = "prima nota",
            IsFavorite = false,
            AddedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedEntryAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        // Issue #3866: `.AsTracking()` is REQUIRED here. The DbContext default is NoTracking
        // (PERF-06), so a plain read hands back a DETACHED entity: the mutations below would reach
        // no change tracker, SaveChangesAsync would write nothing, and the concurrency token this
        // test exists to exercise would never even be compared. This is the documented opt-out for
        // a fixture whose subject IS a tracked read-modify-write.
        var seenByFirst = await firstContext.UserLibraryEntries.AsTracking().FirstAsync(e => e.Id == id);
        var seenBySecond = await secondContext.UserLibraryEntries.AsTracking().FirstAsync(e => e.Id == id);

        seenByFirst.Notes = "modificata dalla prima scheda";
        await firstContext.SaveChangesAsync();

        seenBySecond.Notes = "modificata dalla seconda scheda";
        seenBySecond.IsFavorite = true;

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due scritture concorrenti sulla stessa voce devono escludersi: senza conflitto "
            + "rilevato le note della prima spariscono senza traccia (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedEntryAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        var entry = await repository.GetByIdAsync(id);
        entry.Should().NotBeNull("il seed deve essere leggibile dal repository");

        entry!.UpdateNotes(new Api.BoundedContexts.UserLibrary.Domain.ValueObjects.LibraryNotes("nota aggiornata"));
        await repository.UpdateAsync(entry);

        var write = async () => await context.SaveChangesAsync();

        // Il test che smaschera una conversione fatta a metà: UpdateAsync passa da
        // MapToPersistence + Update() su detached. Se il token non attraversasse il mapper, la
        // WHERE avrebbe xmin = 0, zero righe toccate, e QUESTA scrittura — non contesa da nessuno —
        // fallirebbe con DbUpdateConcurrencyException (#3688).
        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare anche attraverso Update() su grafo detached");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.UserLibraryEntries.FirstAsync(e => e.Id == id);
        persisted.Notes.Should().Be("nota aggiornata");
    }
}
