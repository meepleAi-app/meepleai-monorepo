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
/// #4557 — come <see cref="SharedGameRepository"/> legge e scrive le FAQ dello
/// <c>SharedGame</c>. Stesso schema delle errata (#4543, <see cref="SharedGameRepositoryErrataTests"/>).
/// </summary>
/// <remarks>
/// <para>
/// Il rischio della correzione è che l'update riguarda tutto l'aggregato: un comando che legge il
/// gioco senza le FAQ e lo salva non deve cancellarle, né scrivere nulla su <c>game_faqs</c>. Le
/// asserzioni guardano il change tracker, cioè le istruzioni che <c>SaveChanges</c> emetterà: valgono
/// per qualunque provider. Il database InMemory nasconde invece la perdita dei figli nuovi su un
/// grafo detached: la prova su Postgres sta in <c>SharedGameCatalogEndpointsIntegrationTests.Faq</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameRepositoryFaqTests
{
    private static readonly DateTime Created = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTime Edited = new(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private static SharedGameRepository Repo(MeepleAiDbContext db) =>
        new(db, Mock.Of<IDomainEventCollector>());

    private static async Task<(Guid GameId, GameFaqEntity First, GameFaqEntity Second)> SeedAsync(MeepleAiDbContext db)
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

        var first = NewFaq(gameId, "Si può commerciare col banco?", "Sì, 4 a 1.", displayOrder: 1, upvotes: 3);
        var second = NewFaq(gameId, "Il ladro ruba sempre?", "Solo con un 7.", displayOrder: 2, upvotes: 0);
        db.GameFaqs.AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        return (gameId, first, second);
    }

    private static GameFaqEntity NewFaq(Guid gameId, string question, string answer, int displayOrder, int upvotes) => new()
    {
        Id = Guid.NewGuid(),
        SharedGameId = gameId,
        Question = question,
        Answer = answer,
        DisplayOrder = displayOrder,
        UpvoteCount = upvotes,
        CreatedAt = Created,
        UpdatedAt = Edited,
    };

    private static async Task<Dictionary<Guid, GameFaqEntity>> RowsAsync(MeepleAiDbContext db, Guid gameId) =>
        await db.GameFaqs.AsNoTracking()
            .Where(f => f.SharedGameId == gameId)
            .ToDictionaryAsync(f => f.Id, TestContext.Current.CancellationToken);

    private static IEnumerable<string> ModifiedColumns(MeepleAiDbContext db, Guid faqId) =>
        db.ChangeTracker.Entries<GameFaqEntity>()
            .Single(e => e.Entity.Id == faqId)
            .Properties.Where(p => p.IsModified)
            .Select(p => p.Metadata.Name);

    [Fact]
    public async Task Update_OfAGameReadWithoutItsFaqs_WritesNothingOnTheFaqs()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        // GetByIdAsync non carica le FAQ: è la lettura di quasi ogni comando sullo SharedGame.
        var game = await repo.GetByIdAsync(gameId, TestContext.Current.CancellationToken);
        game!.Faqs.Should().BeEmpty("GetByIdAsync does not load the FAQs");
        repo.Update(game);

        db.ChangeTracker.Entries<GameFaqEntity>().Should().BeEmpty(
            "an update that did not read the FAQs must not emit any statement on game_faqs");

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task Update_AfterAddFaq_OnAGameReadWithoutItsFaqs_InsertsOnlyTheNewRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetByIdAsync(gameId, TestContext.Current.CancellationToken);
        var added = GameFaq.Create(gameId, "Quanti punti per vincere?", "Dieci.", displayOrder: 3);
        game!.AddFaq(added);
        repo.Update(game);

        var tracked = db.ChangeTracker.Entries<GameFaqEntity>().ToList();
        tracked.Should().ContainSingle();
        tracked[0].State.Should().Be(EntityState.Added);
        tracked[0].Entity.Id.Should().Be(added.Id);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([first.Id, second.Id, added.Id]);
        rows[added.Id].Question.Should().Be("Quanti punti per vincere?");
        rows[added.Id].Answer.Should().Be("Dieci.");
        rows[added.Id].DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task GetGameByFaqIdAsync_LoadsEveryFaqOfTheGame_AsSaved()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (_, first, second) = await SeedAsync(db);

        var game = await Repo(db).GetGameByFaqIdAsync(first.Id, TestContext.Current.CancellationToken);

        game.Should().NotBeNull();
        game!.Faqs.Select(f => (f.Id, f.SharedGameId, f.Question, f.Answer, f.DisplayOrder, f.UpvoteCount, f.CreatedAt, f.UpdatedAt))
            .Should().BeEquivalentTo([
                (first.Id, first.SharedGameId, first.Question, first.Answer, first.DisplayOrder, first.UpvoteCount, first.CreatedAt, first.UpdatedAt),
                (second.Id, second.SharedGameId, second.Question, second.Answer, second.DisplayOrder, second.UpvoteCount, second.CreatedAt, second.UpdatedAt),
            ]);
        game.DomainEvents.Should().BeEmpty("reading the FAQs back must not raise GameFaqAddedEvent");
    }

    [Fact]
    public async Task Update_AfterUpdateFaq_ModifiesOnlyTheChangedColumnsOfThatRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetGameByFaqIdAsync(first.Id, TestContext.Current.CancellationToken);
        game!.UpdateFaq(first.Id, "Si può commerciare col banco? (rivista)", first.Answer, first.DisplayOrder);
        repo.Update(game);

        var entries = db.ChangeTracker.Entries<GameFaqEntity>().ToDictionary(e => e.Entity.Id);
        entries[first.Id].State.Should().Be(EntityState.Modified);
        // UpdatedAt lo rimette il dominio a ogni modifica: è una colonna cambiata davvero.
        ModifiedColumns(db, first.Id).Should().BeEquivalentTo(
            [nameof(GameFaqEntity.Question), nameof(GameFaqEntity.UpdatedAt)],
            "the vote count, the order and the creation time must not be rewritten");
        if (entries.TryGetValue(second.Id, out var untouched))
        {
            untouched.State.Should().Be(EntityState.Unchanged);
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows[first.Id].Question.Should().Be("Si può commerciare col banco? (rivista)");
        rows[first.Id].UpvoteCount.Should().Be(3);
        rows[first.Id].CreatedAt.Should().Be(Created);
        rows[second.Id].Question.Should().Be("Il ladro ruba sempre?");
    }

    [Fact]
    public async Task Update_AfterUpvoteFaq_ModifiesOnlyTheCountAndItsTimestamp()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, _) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetGameByFaqIdAsync(first.Id, TestContext.Current.CancellationToken);
        game!.UpvoteFaq(first.Id).Should().Be(4);
        repo.Update(game);

        ModifiedColumns(db, first.Id).Should().BeEquivalentTo(
            [nameof(GameFaqEntity.UpvoteCount), nameof(GameFaqEntity.UpdatedAt)],
            "a vote must not rewrite the question or the answer");

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows[first.Id].UpvoteCount.Should().Be(4);
        rows[first.Id].Question.Should().Be(first.Question);
    }

    [Fact]
    public async Task Update_AfterRemoveFaq_DeletesOnlyThatRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetGameByFaqIdAsync(first.Id, TestContext.Current.CancellationToken);
        game!.RemoveFaq(first.Id);
        repo.Update(game);

        db.ChangeTracker.Entries<GameFaqEntity>()
            .Where(e => e.State != EntityState.Unchanged)
            .Select(e => (e.Entity.Id, e.State))
            .Should().BeEquivalentTo([(first.Id, EntityState.Deleted)]);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([second.Id]);
    }

    [Fact]
    public async Task AddAsync_OfAGameWithFaqs_InsertsThem()
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
        var faq = GameFaq.Create(game.Id, "Il contadino conta doppio?", "No.", displayOrder: 0);
        game.AddFaq(faq);

        await repo.AddAsync(game, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await RowsAsync(db, game.Id);
        rows.Keys.Should().BeEquivalentTo([faq.Id]);
    }
}
