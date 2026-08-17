using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Concorrenza ottimistica su <c>share_requests</c> (#3651, lotto 3).
///
/// Il dominio prevede esplicitamente la race: due amministratori che risolvono la stessa richiesta
/// di condivisione. Senza un token attivo entrambe le risoluzioni riescono e l'ultima sovrascrive
/// la prima — una richiesta approvata può risultare rifiutata, o viceversa, senza che nulla lo
/// segnali.
///
/// Come per <c>GameBook</c>, i test passano dal repository e non dal DbContext, e coprono
/// entrambe le direzioni. Qui conta in particolare perché <see cref="ShareRequestRepository"/>
/// ha <b>due rami</b> di scrittura: se trova un'entità già tracciata la muta in place, altrimenti
/// riattacca un grafo detached con <c>MapToEntity</c> + <c>DbSet.Update()</c>. Il secondo ramo è
/// quello che nel lotto 2 si è rivelato capace di rompere ogni scrittura quando il token non
/// attraversa il mapper.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ShareRequestXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public ShareRequestXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"sharereq_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static ShareRequestRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Semina il gioco sorgente e restituisce una richiesta che lo referenzia.
    /// <c>share_requests.source_game_id</c> è una FK verso <c>shared_games</c> con
    /// <c>DeleteBehavior.Restrict</c>: con un Guid inventato l'INSERT fallisce con
    /// <c>DbUpdateException</c> (23503) prima ancora di arrivare al token di concorrenza,
    /// e il test rosso direbbe la cosa sbagliata (pitfall #2620).
    /// </summary>
    private async Task<ShareRequest> SeedRequestAsync()
    {
        var sourceGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = sourceGameId,
            Title = "Gioco sorgente"
        });
        await _dbContext.SaveChangesAsync();

        return ShareRequest.Create(
            userId: Guid.NewGuid(),
            sourceGameId: sourceGameId,
            contributionType: ContributionType.NewGame,
            userNotes: "Proposta iniziale");
    }

    [Fact(DisplayName = "ShareRequest: un solo scrittore riesce — il token non blocca chi non ha rivali")]
    public async Task Update_WithNoConcurrentWriter_Succeeds()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var repository = CreateRepository(_dbContext);
        var request = await SeedRequestAsync();
        await repository.AddAsync(request);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // ── Act: un admin prende in carico la richiesta ───────────────────────────
        var loaded = await repository.GetByIdAsync(request.Id);
        loaded.Should().NotBeNull();

        loaded!.StartReview(Guid.NewGuid());
        repository.Update(loaded);

        Func<Task> act = async () => await _dbContext.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().NotThrowAsync(
            "senza scritture concorrenti la presa in carico deve passare: se il token letto dal " +
            "DB non attraversa il mapper, l'UPDATE emette `WHERE xmin = 0` e non trova la riga");
    }

    [Fact(DisplayName = "ShareRequest: due admin risolvono la stessa richiesta, il secondo è rifiutato")]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var seedRepository = CreateRepository(_dbContext);
        var request = await SeedRequestAsync();
        await seedRepository.AddAsync(request);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);
        var repoA = CreateRepository(dbA);
        var repoB = CreateRepository(dbB);

        var requestA = await repoA.GetByIdAsync(request.Id);
        var requestB = await repoB.GetByIdAsync(request.Id);
        requestA.Should().NotBeSameAs(requestB, "sono due scope indipendenti sulla stessa riga");

        // ── Act: due admin la prendono in carico nello stesso momento ─────────────
        requestA!.StartReview(Guid.NewGuid());
        repoA.Update(requestA);
        await dbA.SaveChangesAsync();

        requestB!.StartReview(Guid.NewGuid());
        repoB.Update(requestB);
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "il secondo admin ha letto prima del commit del primo: la sua presa in carico va " +
            "rifiutata, altrimenti due amministratori credono entrambi di avere il lock");
    }
}
