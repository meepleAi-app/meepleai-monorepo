using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// #4543 — come <see cref="SharedGameRepository"/> legge e scrive le errata dello
/// <c>SharedGame</c>.
/// </summary>
/// <remarks>
/// <para>
/// Il rischio della correzione è che l'update riguarda tutto l'aggregato: un comando che legge il
/// gioco senza le errata e lo salva non deve cancellarle, né scrivere nulla su
/// <c>game_errata</c>. Le asserzioni guardano il change tracker, cioè le istruzioni che
/// <c>SaveChanges</c> emetterà: valgono per qualunque provider. Il database InMemory nasconde invece
/// la perdita dei figli nuovi su un grafo detached (vedi <c>ef-detached-graph-child-loss</c>): la
/// prova su Postgres sta in <c>SharedGameCatalogEndpointsIntegrationTests.Errata</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameRepositoryErrataTests
{
    private static readonly DateTime Published = new(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Created = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static SharedGameRepository Repo(MeepleAiDbContext db) =>
        new(db, Mock.Of<IDomainEventCollector>());

    private static async Task<(Guid GameId, GameErrataEntity First, GameErrataEntity Second)> SeedAsync(MeepleAiDbContext db)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Catan",
            Description = "desc",
            Status = 2, // Published
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
        });

        var first = NewErrata(gameId, "Prima", "p. 1");
        var second = NewErrata(gameId, "Seconda", "p. 2");
        db.GameErrata.AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        return (gameId, first, second);
    }

    private static GameErrataEntity NewErrata(Guid gameId, string description, string pageReference) => new()
    {
        Id = Guid.NewGuid(),
        SharedGameId = gameId,
        Description = description,
        PageReference = pageReference,
        PublishedDate = Published,
        CreatedAt = Created,
    };

    private static async Task<Dictionary<Guid, GameErrataEntity>> RowsAsync(MeepleAiDbContext db, Guid gameId) =>
        await db.GameErrata.AsNoTracking()
            .Where(e => e.SharedGameId == gameId)
            .ToDictionaryAsync(e => e.Id, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Update_OfAGameReadWithoutItsErrata_WritesNothingOnTheErrata()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        // GetByIdAsync non carica le errata: è la lettura di quasi ogni comando sullo SharedGame.
        var game = await repo.GetByIdAsync(gameId, TestContext.Current.CancellationToken);
        game!.Erratas.Should().BeEmpty("GetByIdAsync does not load the errata");
        repo.Update(game);

        db.ChangeTracker.Entries<GameErrataEntity>().Should().BeEmpty(
            "an update that did not read the errata must not emit any statement on game_errata");

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task Update_AfterAddErrata_OnAGameReadWithoutItsErrata_InsertsOnlyTheNewRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetByIdAsync(gameId, TestContext.Current.CancellationToken);
        var added = GameErrata.Create(gameId, "Terza", "p. 3", Published);
        game!.AddErrata(added);
        repo.Update(game);

        var tracked = db.ChangeTracker.Entries<GameErrataEntity>().ToList();
        tracked.Should().ContainSingle();
        tracked[0].State.Should().Be(EntityState.Added);
        tracked[0].Entity.Id.Should().Be(added.Id);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([first.Id, second.Id, added.Id]);
        rows[added.Id].Description.Should().Be("Terza");
        rows[added.Id].PageReference.Should().Be("p. 3");
        rows[added.Id].PublishedDate.Should().Be(Published);
    }

    [Fact]
    public async Task GetGameByErrataIdAsync_LoadsEveryErrataOfTheGame_AsSaved()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (_, first, second) = await SeedAsync(db);

        var game = await Repo(db).GetGameByErrataIdAsync(first.Id, TestContext.Current.CancellationToken);

        game.Should().NotBeNull();
        game!.Erratas.Select(e => (e.Id, e.SharedGameId, e.Description, e.PageReference, e.PublishedDate, e.CreatedAt))
            .Should().BeEquivalentTo([
                (first.Id, first.SharedGameId, first.Description, first.PageReference, first.PublishedDate, first.CreatedAt),
                (second.Id, second.SharedGameId, second.Description, second.PageReference, second.PublishedDate, second.CreatedAt),
            ]);
        game.DomainEvents.Should().BeEmpty("reading the errata back must not raise GameErrataAddedEvent");
    }

    [Fact]
    public async Task Update_AfterUpdateErrata_ModifiesOnlyTheChangedColumnsOfThatRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetGameByErrataIdAsync(first.Id, TestContext.Current.CancellationToken);
        game!.UpdateErrata(first.Id, "Prima, corretta", first.PageReference, first.PublishedDate);
        repo.Update(game);

        var entries = db.ChangeTracker.Entries<GameErrataEntity>().ToDictionary(e => e.Entity.Id);
        entries[first.Id].State.Should().Be(EntityState.Modified);
        entries[first.Id].Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)
            .Should().BeEquivalentTo([nameof(GameErrataEntity.Description)]);
        if (entries.TryGetValue(second.Id, out var untouched))
        {
            untouched.State.Should().Be(EntityState.Unchanged);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows[first.Id].Description.Should().Be("Prima, corretta");
        rows[first.Id].CreatedAt.Should().Be(Created);
        rows[second.Id].Description.Should().Be("Seconda");
    }

    [Fact]
    public async Task Update_AfterRemoveErrata_DeletesOnlyThatRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetGameByErrataIdAsync(first.Id, TestContext.Current.CancellationToken);
        game!.RemoveErrata(first.Id);
        repo.Update(game);

        db.ChangeTracker.Entries<GameErrataEntity>()
            .Where(e => e.State != EntityState.Unchanged)
            .Select(e => (e.Entity.Id, e.State))
            .Should().BeEquivalentTo([(first.Id, EntityState.Deleted)]);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([second.Id]);
    }

    [Fact]
    public async Task AddAsync_OfAGameWithErrata_InsertsThem()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repo = Repo(db);

        var game = Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame.Create(
            title: "Carcassonne",
            yearPublished: 2000,
            description: "Tiles",
            minPlayers: 2,
            maxPlayers: 5,
            playingTimeMinutes: 45,
            minAge: 7,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/c.jpg",
            thumbnailUrl: "https://example.com/c-thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());
        var errata = GameErrata.Create(game.Id, "Il contadino conta doppio", "p. 12", Published);
        game.AddErrata(errata);

        await repo.AddAsync(game, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await RowsAsync(db, game.Id);
        rows.Keys.Should().BeEquivalentTo([errata.Id]);
    }
}
