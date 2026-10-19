using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Due scritture sulla stessa voce di libreria nello stesso <see cref="MeepleAiDbContext"/> (#4253).
///
/// <para>
/// È il caso di <c>PATCH /api/v1/library/games/{id}</c> con <c>newState</c>: la rotta manda
/// <c>UpdateGameStateCommand</c> e poi <c>UpdateLibraryEntryCommand</c> nella stessa richiesta,
/// quindi nello stesso contesto. <c>UpdateAsync</c> fa <c>MapToPersistence</c> +
/// <c>DbSet.Update</c>: la prima scrittura lascia tracciata la sua istanza anche dopo il
/// salvataggio, e la seconda ne porta un'altra con la stessa chiave. EF rifiuta la seconda con
/// <see cref="InvalidOperationException"/> («another instance with the same key value for {'Id'} is
/// already being tracked»), che il middleware traduce in 500.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4253")]
public sealed class UserLibraryEntrySameContextUpdateTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public UserLibraryEntrySameContextUpdateTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"userlib_samectx_{Guid.NewGuid():N}";
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
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Semina la riga direttamente, con le due FK soddisfatte (pitfall #2620): il percorso di
    /// scrittura del repository è ciò che i test esercitano, non ciò da cui dipendono.
    /// </summary>
    private async Task<(Guid UserId, Guid GameId, Guid EntryId)> SeedEntryAsync()
    {
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

        var entryId = Guid.NewGuid();
        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = entryId,
            UserId = userId,
            GameId = gameId,
            IsFavorite = false,
            AddedAt = DateTime.UtcNow,
            CurrentState = (int)GameStateType.Nuovo,
        });
        await _dbContext.SaveChangesAsync();
        return (userId, gameId, entryId);
    }

    [Fact]
    public async Task SecondUpdate_InSameContext_AfterSave_PersistsBothWrites()
    {
        var (userId, gameId, entryId) = await SeedEntryAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        // Passo 1 della rotta PATCH: UpdateGameStateCommandHandler.
        var first = await repository.GetByUserAndGameAsync(userId, gameId);
        first.Should().NotBeNull("il seed deve essere leggibile dal repository");
        first!.ChangeState(GameState.Owned());
        await repository.UpdateAsync(first);
        await context.SaveChangesAsync();

        // Passo 2 della rotta PATCH: UpdateLibraryEntryCommandHandler, stesso contesto.
        var second = await repository.GetByUserAndGameAsync(userId, gameId);
        second!.SetFavorite(true);

        var write = async () =>
        {
            await repository.UpdateAsync(second);
            await context.SaveChangesAsync();
        };

        await write.Should().NotThrowAsync(
            "la seconda scrittura sulla stessa voce, nello stesso contesto, è quella che la rotta "
            + "PATCH con newState fa sempre: se fallisce, ogni cambio di stato risponde 500 (#4253)");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.UserLibraryEntries.FirstAsync(e => e.Id == entryId);
        persisted.CurrentState.Should().Be((int)GameStateType.Owned, "la prima scrittura non va persa");
        persisted.IsFavorite.Should().BeTrue("la seconda scrittura deve arrivare al database");
    }

    [Fact]
    public async Task SecondUpdate_InSameContext_WithUnsavedFirstWrite_DoesNotDiscardItSilently()
    {
        var (userId, gameId, _) = await SeedEntryAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        var first = await repository.GetByUserAndGameAsync(userId, gameId);
        first!.UpdateNotes(new LibraryNotes("nota non ancora salvata"));
        await repository.UpdateAsync(first);

        // Nessun SaveChanges in mezzo: la copia tracciata porta una modifica pendente. Staccarla
        // la perderebbe senza che nessuno se ne accorga, perché la seconda lettura non la vede.
        var second = await repository.GetByUserAndGameAsync(userId, gameId);
        second!.SetFavorite(true);

        var update = async () => await repository.UpdateAsync(second);

        // GUARDIA, non test del difetto: passa anche senza la correzione di #4253, perché il
        // codice vecchio solleva questo stesso conflitto. Diventa rossa se il distacco diventa
        // incondizionato, cioè se una copia con modifiche pendenti viene staccata in silenzio.
        await update.Should().ThrowAsync<InvalidOperationException>(
                "una copia tracciata con modifiche pendenti non si stacca: il conflitto resta visibile")
            .WithMessage("*already being tracked*");
    }

    [Fact]
    public async Task SecondUpdate_InSameContext_FromStaleRead_IsRejectedByXmin()
    {
        var (userId, gameId, entryId) = await SeedEntryAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        // Due letture della stessa voce PRIMA di qualunque scrittura: entrambe portano lo stesso
        // xmin. Dopo il salvataggio di `a`, quello di `b` non corrisponde più a nessuna tupla viva.
        var a = await repository.GetByUserAndGameAsync(userId, gameId);
        var b = await repository.GetByUserAndGameAsync(userId, gameId);

        a!.ChangeState(GameState.Owned());
        await repository.UpdateAsync(a);
        await context.SaveChangesAsync();

        b!.SetFavorite(true);

        var staleWrite = async () =>
        {
            await repository.UpdateAsync(b);
            await context.SaveChangesAsync();
        };

        // Il distacco di DetachSavedCopy è sicuro solo perché la scrittura nuova porta l'xmin che
        // l'aggregato ha letto: una lettura vecchia deve fallire sul token, non sovrascrivere.
        // Tipo ESATTO: il codice prima di #4253 qui sollevava InvalidOperationException
        // («already being tracked») già in UpdateAsync, che questo test non deve accettare.
        await staleWrite.Should().ThrowExactlyAsync<DbUpdateConcurrencyException>(
            "una scrittura da una lettura superata deve essere rifiutata dal token xmin, "
            + "anche dopo che la copia salvata è stata staccata");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.UserLibraryEntries.FirstAsync(e => e.Id == entryId);
        persisted.CurrentState.Should().Be((int)GameStateType.Owned, "la scrittura di `a` resta");
        persisted.IsFavorite.Should().BeFalse("la scrittura superata di `b` non deve arrivare al database");
    }
}
