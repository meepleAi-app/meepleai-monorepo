using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
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
/// #4596 — come <see cref="SharedGameRepository"/> legge e scrive le quick question dello
/// <c>SharedGame</c>. Stesso schema di errata (#4543) e FAQ (#4557).
/// </summary>
/// <remarks>
/// <para>
/// Il rischio della correzione è che l'update riguarda tutto l'aggregato: un comando che legge il
/// gioco senza le domande e lo salva non deve cancellarle, né scrivere nulla su <c>quick_questions</c>.
/// Le asserzioni guardano il change tracker, cioè le istruzioni che <c>SaveChanges</c> emetterà: valgono
/// per qualunque provider. Il database InMemory nasconde invece la perdita dei figli nuovi su un grafo
/// detached: la prova su Postgres sta in <c>SharedGameCatalogEndpointsIntegrationTests.QuickQuestions</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4596")]
public sealed class SharedGameRepositoryQuickQuestionTests
{
    private static readonly DateTime Created = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static SharedGameRepository Repo(MeepleAiDbContext db) =>
        new(db, Mock.Of<IDomainEventCollector>());

    private static async Task<(Guid GameId, QuickQuestionEntity First, QuickQuestionEntity Second)> SeedAsync(MeepleAiDbContext db)
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

        var first = NewQuestion(gameId, "Come si prepara la partita?", QuestionCategory.Setup, displayOrder: 0, isGenerated: true);
        var second = NewQuestion(gameId, "Si può commerciare col banco?", QuestionCategory.Rules, displayOrder: 1, isGenerated: false);
        db.QuickQuestions.AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        return (gameId, first, second);
    }

    private static QuickQuestionEntity NewQuestion(
        Guid gameId, string text, QuestionCategory category, int displayOrder, bool isGenerated) => new()
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Text = text,
            Emoji = "🎮",
            Category = category,
            DisplayOrder = displayOrder,
            IsGenerated = isGenerated,
            CreatedAt = Created.AddMinutes(displayOrder),
            IsActive = displayOrder == 0,
        };

    private static async Task<Dictionary<Guid, QuickQuestionEntity>> RowsAsync(MeepleAiDbContext db, Guid gameId) =>
        await db.QuickQuestions.AsNoTracking()
            .Where(q => q.SharedGameId == gameId)
            .ToDictionaryAsync(q => q.Id, TestContext.Current.CancellationToken);

    private static IEnumerable<string> ModifiedColumns(MeepleAiDbContext db, Guid questionId) =>
        db.ChangeTracker.Entries<QuickQuestionEntity>()
            .Single(e => e.Entity.Id == questionId)
            .Properties.Where(p => p.IsModified)
            .Select(p => p.Metadata.Name);

    [Fact]
    public async Task Update_OfAGameReadWithoutItsQuickQuestions_WritesNothingOnThem()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        // GetByIdAsync non carica le domande: è la lettura di quasi ogni comando sullo SharedGame.
        var game = await repo.GetByIdAsync(gameId, TestContext.Current.CancellationToken);
        game!.QuickQuestions.Should().BeEmpty("GetByIdAsync does not load the quick questions");
        repo.Update(game);

        db.ChangeTracker.Entries<QuickQuestionEntity>().Should().BeEmpty(
            "an update that did not read the quick questions must not emit any statement on quick_questions");

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task Update_AfterAddQuickQuestion_OnAGameReadWithoutThem_InsertsOnlyTheNewRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetByIdAsync(gameId, TestContext.Current.CancellationToken);
        var added = QuickQuestion.CreateManual(gameId, "Quanti punti per vincere?", "🏆", QuestionCategory.Winning, displayOrder: 2);
        game!.AddQuickQuestion(added);
        repo.Update(game);

        var tracked = db.ChangeTracker.Entries<QuickQuestionEntity>().ToList();
        tracked.Should().ContainSingle();
        tracked[0].State.Should().Be(EntityState.Added);
        tracked[0].Entity.Id.Should().Be(added.Id);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([first.Id, second.Id, added.Id]);
        rows[added.Id].Text.Should().Be("Quanti punti per vincere?");
        rows[added.Id].Emoji.Should().Be("🏆");
        rows[added.Id].Category.Should().Be(QuestionCategory.Winning);
        rows[added.Id].DisplayOrder.Should().Be(2);
        rows[added.Id].IsGenerated.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdWithQuickQuestionsAsync_LoadsEveryQuestionOfTheGame_AsSaved()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);

        var game = await Repo(db).GetByIdWithQuickQuestionsAsync(gameId, TestContext.Current.CancellationToken);

        game.Should().NotBeNull();
        // Gli id, IsGenerated, CreatedAt e IsActive sono quelli salvati: una ricostruzione con
        // CreateManual/CreateFromAI li rigenererebbe.
        game!.QuickQuestions
            .Select(q => (q.Id, q.SharedGameId, q.Text, q.Emoji, q.Category, q.DisplayOrder, q.IsGenerated, q.CreatedAt, q.IsActive))
            .Should().BeEquivalentTo([
                (first.Id, first.SharedGameId, first.Text, first.Emoji, first.Category, first.DisplayOrder, first.IsGenerated, first.CreatedAt, first.IsActive),
                (second.Id, second.SharedGameId, second.Text, second.Emoji, second.Category, second.DisplayOrder, second.IsGenerated, second.CreatedAt, second.IsActive),
            ]);
    }

    [Fact]
    public async Task GetByIdWithQuickQuestionsAsync_OfADeletedGame_ReturnsNull()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, _, _) = await SeedAsync(db);
        var row = await db.SharedGames.AsTracking().IgnoreQueryFilters().SingleAsync(g => g.Id == gameId, TestContext.Current.CancellationToken);
        row.IsDeleted = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var game = await Repo(db).GetByIdWithQuickQuestionsAsync(gameId, TestContext.Current.CancellationToken);

        game.Should().BeNull();
    }

    [Fact]
    public async Task Update_AfterReadingTheQuestionsWithoutChangingThem_EmitsNothingOnThem()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, _, _) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetByIdWithQuickQuestionsAsync(gameId, TestContext.Current.CancellationToken);
        repo.Update(game!);

        db.ChangeTracker.Entries<QuickQuestionEntity>()
            .Where(e => e.State != EntityState.Unchanged)
            .Should().BeEmpty("a save that changes no question must not write any row of quick_questions");
    }

    [Fact]
    public async Task Update_AfterEditingAQuestion_ModifiesOnlyTheChangedColumnsOfThatRow()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetByIdWithQuickQuestionsAsync(gameId, TestContext.Current.CancellationToken);
        game!.QuickQuestions.Single(q => q.Id == first.Id).UpdateText("Come si prepara la partita? (rivista)");
        repo.Update(game);

        var entries = db.ChangeTracker.Entries<QuickQuestionEntity>().ToDictionary(e => e.Entity.Id);
        entries[first.Id].State.Should().Be(EntityState.Modified);
        ModifiedColumns(db, first.Id).Should().BeEquivalentTo(
            [nameof(QuickQuestionEntity.Text)],
            "the category, the order, the origin and the creation time must not be rewritten");
        entries[second.Id].State.Should().Be(EntityState.Unchanged);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows[first.Id].Text.Should().Be("Come si prepara la partita? (rivista)");
        rows[first.Id].IsGenerated.Should().BeTrue();
        rows[first.Id].CreatedAt.Should().Be(first.CreatedAt);
        rows[second.Id].Text.Should().Be(second.Text);
    }

    [Fact]
    public async Task Update_AfterRemoveQuickQuestion_DeletesOnlyThatRow_EvenIfTheRowWasQueriedUntracked()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        // Come DeleteQuickQuestionCommandHandler: prima trova la riga con una query senza tracking per
        // ricavare il gioco, poi legge l'aggregato, toglie la domanda e salva. La riga non deve tornare.
        var found = await db.QuickQuestions.AsNoTracking()
            .FirstAsync(q => q.Id == first.Id, TestContext.Current.CancellationToken);
        var game = await repo.GetByIdWithQuickQuestionsAsync(found.SharedGameId, TestContext.Current.CancellationToken);
        game!.RemoveQuickQuestion(first.Id);
        repo.Update(game);

        db.ChangeTracker.Entries<QuickQuestionEntity>()
            .Where(e => e.State != EntityState.Unchanged)
            .Select(e => (e.Entity.Id, e.State))
            .Should().BeEquivalentTo([(first.Id, EntityState.Deleted)]);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([second.Id]);
    }

    [Fact]
    public async Task Update_AfterReplaceQuickQuestions_DeletesTheOldRowsAndInsertsTheNewOnes()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (gameId, first, second) = await SeedAsync(db);
        var repo = Repo(db);

        var game = await repo.GetByIdWithQuickQuestionsAsync(gameId, TestContext.Current.CancellationToken);
        var generated = new[]
        {
            QuickQuestion.CreateFromAI(gameId, "Come si vince?", QuestionCategory.Winning, displayOrder: 0),
            QuickQuestion.CreateFromAI(gameId, "Quando finisce il turno?", QuestionCategory.Gameplay, displayOrder: 1),
        };
        game!.ReplaceQuickQuestions(generated);
        repo.Update(game);

        db.ChangeTracker.Entries<QuickQuestionEntity>()
            .Select(e => (e.Entity.Id, e.State))
            .Should().BeEquivalentTo([
                (first.Id, EntityState.Deleted),
                (second.Id, EntityState.Deleted),
                (generated[0].Id, EntityState.Added),
                (generated[1].Id, EntityState.Added),
            ]);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var rows = await RowsAsync(db, gameId);
        rows.Keys.Should().BeEquivalentTo([generated[0].Id, generated[1].Id]);
        rows.Values.Should().OnlyContain(q => q.IsGenerated);
    }

    [Fact]
    public async Task AddAsync_OfAGameWithQuickQuestions_InsertsThem()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repo = Repo(db);

        var game = SharedGame.Create(
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
        var question = QuickQuestion.CreateManual(game.Id, "Il contadino conta doppio?", "❓", QuestionCategory.Clarifications, displayOrder: 0);
        game.AddQuickQuestion(question);

        await repo.AddAsync(game, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var rows = await RowsAsync(db, game.Id);
        rows.Keys.Should().BeEquivalentTo([question.Id]);
    }
}
