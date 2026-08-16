using Api.BoundedContexts.UserLibrary.Domain.Entities;
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
/// Concorrenza ottimistica su <c>ProposalMigrations</c> (#3651, lotto 7).
///
/// <para>
/// La riga registra la scelta dell'utente dopo che una proposta è stata approvata: collegare la
/// copia privata al catalogo, oppure tenerla privata. È una decisione one-shot che due percorsi
/// possono toccare insieme — la scelta dell'utente e un handler che reagisce all'approvazione.
/// Senza token attivo l'ultima scrittura vince, e una scelta esplicita può essere sovrascritta da
/// un automatismo senza che nulla lo segnali.
/// </para>
/// <para>
/// Come per <c>UserLibraryEntry</c> (lotto 6), <c>ProposalMigrationRepository.UpdateAsync</c> ha
/// <b>due rami</b>: se l'entità è già tracciata usa <c>CurrentValues.SetValues</c>, altrimenti
/// riattacca con <c>Update()</c> su un grafo <b>detached</b> (<c>:97</c>). Il secondo è quello che
/// richiede il round-trip del token, ed è la ragione per cui il test non-conteso conta quanto
/// quello conteso.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "3651")]
public sealed class ProposalMigrationXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public ProposalMigrationXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"proposalmig_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static ProposalMigrationRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// `PrivateGameId` è l'unica FK reale della tabella (<c>HasOne(e =&gt; e.PrivateGame)</c>,
    /// DeleteBehavior.Restrict): senza la riga referenziata l'INSERT fallisce con 23503 e il test
    /// rosso direbbe la cosa sbagliata (pitfall #2620). `ShareRequestId`, `SharedGameId` e `UserId`
    /// sono solo `IsRequired()`, senza vincolo referenziale.
    /// </summary>
    private async Task<Guid> SeedMigrationAsync()
    {
        var ownerId = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = ownerId,
            Email = $"proprietario-{ownerId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });

        var privateGameId = Guid.NewGuid();
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = privateGameId,
            OwnerId = ownerId,
            BggId = 424242,
            // chk_private_games_players: min_players > 0 AND max_players >= min_players.
            // I default a 0 violano il vincolo, e il fallimento (23514) arriverebbe prima del
            // token, facendo dire al test rosso la cosa sbagliata.
            MinPlayers = 2,
            MaxPlayers = 4,
        });
        await _dbContext.SaveChangesAsync();

        var migration = ProposalMigration.Create(
            shareRequestId: Guid.NewGuid(),
            privateGameId: privateGameId,
            sharedGameId: Guid.NewGuid(),
            userId: Guid.NewGuid());

        await CreateRepository(_dbContext).AddAsync(migration);
        await _dbContext.SaveChangesAsync();

        return migration.Id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedMigrationAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        var seenByFirst = await firstContext.Set<ProposalMigrationEntity>().FirstAsync(e => e.Id == id);
        var seenBySecond = await secondContext.Set<ProposalMigrationEntity>().FirstAsync(e => e.Id == id);

        seenByFirst.Choice = 1; // LinkToCatalog
        seenByFirst.ChoiceAt = DateTime.UtcNow;
        await firstContext.SaveChangesAsync();

        // Il secondo percorso decide l'opposto partendo da una riga già superata.
        seenBySecond.Choice = 2; // KeepPrivate
        seenBySecond.ChoiceAt = DateTime.UtcNow;

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due scelte concorrenti sulla stessa migrazione devono escludersi: senza conflitto "
            + "rilevato la seconda sovrascrive la prima e la decisione dell'utente sparisce (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedMigrationAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        var migration = await repository.GetByIdAsync(id);
        migration.Should().NotBeNull();

        migration!.ChooseLinkToCatalog();
        await repository.UpdateAsync(migration);

        var write = async () => await context.SaveChangesAsync();

        // Il ramo detached di UpdateAsync (:97) è quello che smaschera una conversione a metà:
        // senza il token nel mapper la WHERE avrebbe xmin = 0 e questa scrittura, che non ha
        // rivali, fallirebbe comunque (#3688).
        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare anche attraverso Update() su grafo detached");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.Set<ProposalMigrationEntity>().FirstAsync(e => e.Id == id);
        persisted.Choice.Should().Be(1);
    }
}
