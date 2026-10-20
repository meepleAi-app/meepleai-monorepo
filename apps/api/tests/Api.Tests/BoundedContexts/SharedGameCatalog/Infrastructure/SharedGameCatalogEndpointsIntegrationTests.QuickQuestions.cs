using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Il generatore di quick question dell'host di test: nessuna chiamata a un LLM, risposta fissa.
/// </summary>
/// <remarks>
/// Sostituisce <c>LlmQuickQuestionGenerator</c> nella fixture della classe
/// (<see cref="SharedGameCatalogEndpointsHostFixture"/>). Senza stato: lo registra uno scope per
/// richiesta e tutti i test della classe vedono le stesse tre domande.
/// </remarks>
internal sealed class FixedQuickQuestionGenerator : IQuickQuestionGenerator
{
    internal static readonly (string Text, QuestionCategory Category)[] Questions =
    [
        ("Come si prepara la partita?", QuestionCategory.Setup),
        ("Come si vince?", QuestionCategory.Winning),
        ("Cosa si fa nel proprio turno?", QuestionCategory.Gameplay),
    ];

    public Task<QuickQuestionGenerationResult> GenerateQuestionsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new QuickQuestionGenerationResult(
            Questions.Select(q => new GeneratedQuestion(q.Text, q.Category, 0.9m)).ToList(),
            0.9m));
}

/// <summary>
/// #4596 — le quick question di un gioco del catalogo, via HTTP e verificate sul database.
/// </summary>
/// <remarks>
/// <para>
/// Stesso difetto delle errata (#4543) e delle FAQ (#4557): <c>SharedGameRepository</c> non nominava
/// le quick question. <c>MapToDomain</c> non le caricava, quindi l'aggregato le aveva sempre vuote;
/// <c>Update</c> non le scriveva. Il POST manuale e la rigenerazione rispondevano 201 senza salvare
/// niente, il PUT 404 su una riga che esiste, il DELETE sbatteva contro l'aggregato vuoto.
/// </para>
/// <para>
/// I codici HTTP da soli non bastano: il POST rispondeva già 201 e la rigenerazione restituiva le
/// domande che aveva appena costruito in memoria. Ogni test asserisce le righe di
/// <c>quick_questions</c>. <c>AddQuickQuestion_KeepsTheQuestionsAlreadySaved</c> e
/// <c>AnotherUpdateOfTheGame_KeepsItsQuickQuestions</c> fissano il rischio della correzione: l'update
/// di uno <c>SharedGame</c> riguarda tutto l'aggregato, e un comando che non carica le domande non
/// deve cancellarle.
/// </para>
/// <para>
/// La rigenerazione usa <see cref="FixedQuickQuestionGenerator"/>: nessuna chiamata a un LLM a
/// pagamento. Classe parziale per riusare l'host della classe (#4050).
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    private static readonly DateTime QuickQuestionCreated = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public async Task AddQuickQuestion_WritesTheRow_AndTheListShowsIt()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/quick-questions",
            token,
            new { text = "Quanti giocatori servono?", emoji = "🎲", category = (int)QuestionCategory.Rules, displayOrder = 3 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var questionId = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);

        var rows = await QuickQuestionRowsAsync(game.Id);
        rows.Should().ContainSingle("the 201 must correspond to a row in quick_questions, not just an id");
        var row = rows[0];
        row.Id.Should().Be(questionId);
        row.Text.Should().Be("Quanti giocatori servono?");
        row.Emoji.Should().Be("🎲");
        row.Category.Should().Be(QuestionCategory.Rules);
        row.DisplayOrder.Should().Be(3);
        row.IsGenerated.Should().BeFalse("a question written by an admin is not AI-generated");
        row.IsActive.Should().BeTrue();

        // La lista pubblica legge la tabella: la domanda deve comparire.
        using var list = await _client.GetAsync($"/api/v1/games/{game.Id}/quick-questions", TestContext.Current.CancellationToken);
        var listBody = await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        list.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        using var json = JsonDocument.Parse(listBody);
        json.RootElement.EnumerateArray()
            .Select(q => (Id: q.GetProperty("id").GetGuid(), Text: q.GetProperty("text").GetString()))
            .Should().BeEquivalentTo([(questionId, "Quanti giocatori servono?")]);
    }

    [Fact]
    public async Task AddQuickQuestion_KeepsTheQuestionsAlreadySaved()
    {
        // Il POST legge il gioco con GetByIdAsync: se quella lettura non porta le domande già salvate
        // nell'aggregato, l'update non deve trattarle come tolte.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var existing = await SeedQuickQuestionAsync(game.Id, "Già salvata", QuestionCategory.Setup, displayOrder: 0);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/quick-questions",
            token,
            new { text = "Nuova", emoji = "🎲", category = (int)QuestionCategory.Rules, displayOrder = 1 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var rows = (await QuickQuestionRowsAsync(game.Id)).ToDictionary(r => r.Id);
        rows.Should().HaveCount(2);
        AssertQuickQuestionUnchanged(rows[existing.Id], existing);
    }

    [Fact]
    public async Task UpdateQuickQuestion_ChangesTheRow_AndLeavesTheOthersAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedQuickQuestionAsync(game.Id, "Testo originale", QuestionCategory.Setup, displayOrder: 0);
        var other = await SeedQuickQuestionAsync(game.Id, "Un'altra domanda", QuestionCategory.Winning, displayOrder: 1);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/quick-questions/{target.Id}",
            token,
            new { text = "Testo corretto", emoji = "📖", category = (int)QuestionCategory.Rules, displayOrder = 5 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var rows = (await QuickQuestionRowsAsync(game.Id)).ToDictionary(r => r.Id);
        rows.Should().HaveCount(2);

        var updated = rows[target.Id];
        updated.Text.Should().Be("Testo corretto");
        updated.Category.Should().Be(QuestionCategory.Rules);
        updated.DisplayOrder.Should().Be(5);
        updated.IsGenerated.Should().Be(target.IsGenerated, "an update does not change who wrote the question");
        updated.CreatedAt.Should().BeCloseTo(target.CreatedAt, TimeSpan.FromMilliseconds(1),
            "an update keeps the creation timestamp");

        AssertQuickQuestionUnchanged(rows[other.Id], other);
    }

    [Fact]
    public async Task UpdateQuickQuestion_OnAQuestionThatDoesNotExist_Returns404()
    {
        var token = await CreateAdminTokenAsync();

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/quick-questions/{Guid.NewGuid()}",
            token,
            new { text = "Testo", emoji = "📖", category = (int)QuestionCategory.Rules, displayOrder = 0 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteQuickQuestion_RemovesTheRow_AndLeavesTheOthersAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedQuickQuestionAsync(game.Id, "Da togliere", QuestionCategory.Setup, displayOrder: 0);
        var other = await SeedQuickQuestionAsync(game.Id, "Da tenere", QuestionCategory.Winning, displayOrder: 1);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/admin/quick-questions/{target.Id}",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        // Il DELETE passa da GetByIdAsync/Update come gli altri: la riga tolta non deve tornare.
        var rows = await QuickQuestionRowsAsync(game.Id);
        rows.Should().ContainSingle();
        AssertQuickQuestionUnchanged(rows[0], other);
    }

    [Fact]
    public async Task GenerateQuickQuestions_ReplacesTheOldOnes_WithTheGeneratedOnes()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var oldGenerated = await SeedQuickQuestionAsync(game.Id, "Vecchia generata", QuestionCategory.Setup, displayOrder: 0, isGenerated: true);
        var oldManual = await SeedQuickQuestionAsync(game.Id, "Vecchia manuale", QuestionCategory.Rules, displayOrder: 1);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/quick-questions/generate",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var rows = await QuickQuestionRowsAsync(game.Id);
        rows.Select(r => r.Id).Should().NotContain([oldGenerated.Id, oldManual.Id],
            "regenerating replaces the old questions, generated or not");
        rows.Select(r => (r.Text, r.Category, r.DisplayOrder))
            .Should().Equal(FixedQuickQuestionGenerator.Questions.Select((q, i) => (q.Text, q.Category, i)),
                "the rows must be the generated questions, in order");
        rows.Should().OnlyContain(r => r.IsGenerated && r.IsActive && r.SharedGameId == game.Id);

        // Le domande che il comando restituisce sono quelle salvate, con gli stessi id.
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("questions").EnumerateArray()
            .Select(q => q.GetProperty("id").GetGuid())
            .Should().BeEquivalentTo(rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GenerateQuickQuestions_OnAGameWithNoQuestions_InsertsTheGeneratedOnes()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/quick-questions/generate",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        (await QuickQuestionRowsAsync(game.Id)).Select(r => r.Text)
            .Should().Equal(FixedQuickQuestionGenerator.Questions.Select(q => q.Text));
    }

    [Fact]
    public async Task GenerateQuickQuestions_LeavesTheQuestionsOfAnotherGameAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var othersQuestion = await SeedQuickQuestionAsync(otherGame.Id, "Del gioco B", QuestionCategory.Setup, displayOrder: 0);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/quick-questions/generate",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var rows = await QuickQuestionRowsAsync(otherGame.Id);
        rows.Should().ContainSingle();
        AssertQuickQuestionUnchanged(rows[0], othersQuestion);
    }

    [Fact]
    public async Task AnotherUpdateOfTheGame_KeepsItsQuickQuestions()
    {
        // Un comando qualsiasi che legge e salva lo SharedGame senza toccare le domande:
        // submit-for-approval passa da GetByIdAsync + Update.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);
        var existing = await SeedQuickQuestionAsync(game.Id, "Deve restare", QuestionCategory.Setup, displayOrder: 0);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = await QuickQuestionRowsAsync(game.Id);
        rows.Should().ContainSingle();
        AssertQuickQuestionUnchanged(rows[0], existing);
    }

    /// <summary>
    /// Due modifiche della stessa domanda da letture parallele: vince la prima, la seconda fallisce.
    /// </summary>
    /// <remarks>
    /// Le quick question non hanno un token di concorrenza proprio. Le protegge l'UPDATE della radice,
    /// che <c>Update</c> emette sempre (su un grafo detached marca ogni colonna come modificata) con il
    /// controllo su <c>xmin</c> (#3651). Come per errata e FAQ, se un giorno l'update della radice non
    /// scrivesse colonne, la seconda modifica sovrascriverebbe la prima in silenzio.
    /// </remarks>
    [Fact]
    public async Task ConcurrentQuickQuestionUpdates_TheSecondSaveFailsOnTheGameXmin()
    {
        var game = await CreateTestGameAsync(GameStatus.Published);
        var question = await SeedQuickQuestionAsync(game.Id, "Originale", QuestionCategory.Setup, displayOrder: 0);
        var ct = TestContext.Current.CancellationToken;

        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var repoA = scopeA.ServiceProvider.GetRequiredService<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameA = await repoA.GetByIdWithQuickQuestionsAsync(game.Id, ct);
        var gameB = await repoB.GetByIdWithQuickQuestionsAsync(game.Id, ct);

        gameA!.QuickQuestions.Single(q => q.Id == question.Id).UpdateText("Scritta da A");
        repoA.Update(gameA);
        await dbA.SaveChangesAsync(ct);

        gameB!.QuickQuestions.Single(q => q.Id == question.Id).UpdateText("Scritta da B");
        repoB.Update(gameB);
        var saveB = () => dbB.SaveChangesAsync(ct);

        await saveB.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "B read the game before A saved it: its write must not overwrite A's");
        (await QuickQuestionRowsAsync(game.Id)).Single().Text.Should().Be("Scritta da A");
    }

    private async Task<QuickQuestionEntity> SeedQuickQuestionAsync(
        Guid gameId,
        string text,
        QuestionCategory category,
        int displayOrder,
        bool isGenerated = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var row = new QuickQuestionEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Text = text,
            Emoji = "🎮",
            Category = category,
            DisplayOrder = displayOrder,
            IsGenerated = isGenerated,
            CreatedAt = QuickQuestionCreated.AddMinutes(displayOrder),
            IsActive = true,
        };
        db.QuickQuestions.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row;
    }

    private async Task<List<QuickQuestionEntity>> QuickQuestionRowsAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.QuickQuestions
            .AsNoTracking()
            .Where(q => q.SharedGameId == gameId)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertQuickQuestionUnchanged(QuickQuestionEntity actual, QuickQuestionEntity expected)
    {
        actual.Id.Should().Be(expected.Id);
        actual.Text.Should().Be(expected.Text);
        actual.Emoji.Should().Be(expected.Emoji);
        actual.Category.Should().Be(expected.Category);
        actual.DisplayOrder.Should().Be(expected.DisplayOrder);
        actual.IsGenerated.Should().Be(expected.IsGenerated);
        actual.IsActive.Should().Be(expected.IsActive);
        actual.CreatedAt.Should().BeCloseTo(expected.CreatedAt, TimeSpan.FromMilliseconds(1));
    }
}
