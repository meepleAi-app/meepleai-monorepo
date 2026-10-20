using System.Net;
using System.Net.Http.Json;
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
/// #4557 — le FAQ di un gioco del catalogo, via HTTP e verificate sul database.
/// </summary>
/// <remarks>
/// <para>
/// Stesso difetto delle errata prima di #4543: il POST rispondeva 201 senza scrivere la riga,
/// perché <c>SharedGameRepository</c> non mappava le FAQ; PUT, DELETE e il voto rispondevano 404 su
/// una riga che esiste, perché la lettura non caricava le FAQ nell'aggregato.
/// </para>
/// <para>
/// I codici HTTP da soli non bastano: il POST rispondeva già 201. Ogni test asserisce la riga in
/// <c>game_faqs</c>. <c>AddFaq_KeepsTheFaqsAlreadySaved</c> e <c>AnotherUpdateOfTheGame_KeepsItsFaqs</c>
/// fissano il rischio della correzione: l'update di uno <c>SharedGame</c> riguarda tutto l'aggregato,
/// e un comando che non carica le FAQ non deve cancellarle.
/// </para>
/// <para>
/// Classe parziale per riusare l'host della classe (#4050). Gli helper <c>CreateAdminTokenAsync</c>
/// e <c>CreateTestGameAsync</c> stanno negli altri file della classe.
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    private static readonly DateTime FaqCreated = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public async Task AddFaq_WritesTheRow()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/faq",
            token,
            new { question = "Si può commerciare col banco?", answer = "Sì, 4 a 1.", order = 2 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var faqId = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);

        var rows = await FaqRowsAsync(game.Id);
        rows.Should().ContainSingle("the 201 must correspond to a row in game_faqs, not just an id");
        var row = rows[0];
        row.Id.Should().Be(faqId);
        row.Question.Should().Be("Si può commerciare col banco?");
        row.Answer.Should().Be("Sì, 4 a 1.");
        row.DisplayOrder.Should().Be(2);
        row.UpvoteCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateFaq_ChangesTheRow_AndLeavesTheOthersAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedFaqAsync(game.Id, "Domanda originale", "Risposta originale", displayOrder: 1, upvotes: 5);
        var other = await SeedFaqAsync(game.Id, "Un'altra domanda", "Un'altra risposta", displayOrder: 2, upvotes: 1);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/faq/{target.Id}",
            token,
            new { question = "Domanda corretta", answer = "Risposta corretta", order = 7 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = (await FaqRowsAsync(game.Id)).ToDictionary(r => r.Id);
        rows.Should().HaveCount(2);

        var updated = rows[target.Id];
        updated.Question.Should().Be("Domanda corretta");
        updated.Answer.Should().Be("Risposta corretta");
        updated.DisplayOrder.Should().Be(7);
        updated.UpvoteCount.Should().Be(5, "editing the text keeps the votes");
        updated.CreatedAt.Should().BeCloseTo(target.CreatedAt, TimeSpan.FromMilliseconds(1),
            "an update keeps the creation timestamp");
        updated.UpdatedAt.Should().NotBeNull();

        AssertFaqUnchanged(rows[other.Id], other);
    }

    [Fact]
    public async Task DeleteFaq_RemovesTheRow_AndLeavesTheOthersAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedFaqAsync(game.Id, "Da togliere", "x", displayOrder: 1, upvotes: 0);
        var other = await SeedFaqAsync(game.Id, "Da tenere", "y", displayOrder: 2, upvotes: 0);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/admin/shared-games/{game.Id}/faq/{target.Id}",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = await FaqRowsAsync(game.Id);
        rows.Should().ContainSingle();
        AssertFaqUnchanged(rows[0], other);
    }

    [Fact]
    public async Task UpvoteFaq_IncrementsTheCount_AndLeavesTheTextAlone()
    {
        // Il voto è pubblico e anonimo, e passava dalla stessa lettura: rispondeva 404 anche lui.
        var game = await CreateTestGameAsync(GameStatus.Published);
        var faq = await SeedFaqAsync(game.Id, "Domanda votata", "Risposta votata", displayOrder: 1, upvotes: 3);

        using var response = await _client.PostAsync(
            $"/api/v1/faqs/{faq.Id}/upvote", content: null, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var row = (await FaqRowsAsync(game.Id)).Single();
        row.UpvoteCount.Should().Be(4);
        row.Question.Should().Be(faq.Question);
        row.Answer.Should().Be(faq.Answer);
        row.DisplayOrder.Should().Be(faq.DisplayOrder);
    }

    [Fact]
    public async Task AddFaq_KeepsTheFaqsAlreadySaved()
    {
        // Il POST legge il gioco con GetByIdAsync, che non carica le FAQ: quelle salvate non sono
        // nell'aggregato e l'update non deve trattarle come tolte.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var existing = await SeedFaqAsync(game.Id, "Già salvata", "x", displayOrder: 1, upvotes: 2);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/faq",
            token,
            new { question = "Nuova", answer = "y", order = 2 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var rows = (await FaqRowsAsync(game.Id)).ToDictionary(r => r.Id);
        rows.Should().HaveCount(2);
        AssertFaqUnchanged(rows[existing.Id], existing);
    }

    [Fact]
    public async Task AnotherUpdateOfTheGame_KeepsItsFaqs()
    {
        // Un comando qualsiasi che legge e salva lo SharedGame senza toccare le FAQ:
        // submit-for-approval passa da GetByIdAsync + Update.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);
        var existing = await SeedFaqAsync(game.Id, "Deve restare", "x", displayOrder: 1, upvotes: 4);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = await FaqRowsAsync(game.Id);
        rows.Should().ContainSingle();
        AssertFaqUnchanged(rows[0], existing);
    }

    [Fact]
    public async Task UpdateFaq_UnderAnotherGame_Returns404_AndChangesNothing()
    {
        // Il segmento {id} arriva all'handler: una FAQ di un altro gioco non si tocca.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var faq = await SeedFaqAsync(otherGame.Id, "Del gioco B", "x", displayOrder: 1, upvotes: 0);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/faq/{faq.Id}",
            token,
            new { question = "Scritta dal gioco A", answer = "y", order = 2 });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var rows = await FaqRowsAsync(otherGame.Id);
        rows.Should().ContainSingle();
        AssertFaqUnchanged(rows[0], faq);
    }

    [Fact]
    public async Task DeleteFaq_UnderAnotherGame_Returns404_AndKeepsTheRow()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var faq = await SeedFaqAsync(otherGame.Id, "Del gioco B", "x", displayOrder: 1, upvotes: 0);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/admin/shared-games/{game.Id}/faq/{faq.Id}",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await FaqRowsAsync(otherGame.Id)).Should().ContainSingle();
    }

    /// <summary>
    /// Due modifiche della stessa FAQ da letture parallele: vince la prima, la seconda fallisce.
    /// </summary>
    /// <remarks>
    /// Le FAQ non hanno un token di concorrenza proprio. Le protegge l'UPDATE della radice, che
    /// <c>Update</c> emette sempre (su un grafo detached marca ogni colonna come modificata) con il
    /// controllo su <c>xmin</c> (#3651). Come per le errata, se un giorno l'update della radice non
    /// scrivesse colonne, la seconda modifica sovrascriverebbe la prima in silenzio.
    /// </remarks>
    [Fact]
    public async Task ConcurrentFaqUpdates_TheSecondSaveFailsOnTheGameXmin()
    {
        var game = await CreateTestGameAsync(GameStatus.Published);
        var faq = await SeedFaqAsync(game.Id, "Originale", "x", displayOrder: 1, upvotes: 0);
        var ct = TestContext.Current.CancellationToken;

        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var repoA = scopeA.ServiceProvider.GetRequiredService<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<Api.BoundedContexts.SharedGameCatalog.Domain.Repositories.ISharedGameRepository>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameA = await repoA.GetGameByFaqIdAsync(faq.Id, ct);
        var gameB = await repoB.GetGameByFaqIdAsync(faq.Id, ct);

        gameA!.UpdateFaq(faq.Id, "Scritta da A", "x", 1);
        repoA.Update(gameA);
        await dbA.SaveChangesAsync(ct);

        gameB!.UpdateFaq(faq.Id, "Scritta da B", "x", 1);
        repoB.Update(gameB);
        var saveB = () => dbB.SaveChangesAsync(ct);

        await saveB.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "B read the game before A saved it: its write must not overwrite A's");
        (await FaqRowsAsync(game.Id)).Single().Question.Should().Be("Scritta da A");
    }

    private async Task<GameFaqEntity> SeedFaqAsync(Guid gameId, string question, string answer, int displayOrder, int upvotes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var row = new GameFaqEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Question = question,
            Answer = answer,
            DisplayOrder = displayOrder,
            UpvoteCount = upvotes,
            CreatedAt = FaqCreated.AddMinutes(displayOrder),
        };
        db.GameFaqs.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row;
    }

    private async Task<List<GameFaqEntity>> FaqRowsAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.GameFaqs
            .AsNoTracking()
            .Where(f => f.SharedGameId == gameId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertFaqUnchanged(GameFaqEntity actual, GameFaqEntity expected)
    {
        actual.Question.Should().Be(expected.Question);
        actual.Answer.Should().Be(expected.Answer);
        actual.DisplayOrder.Should().Be(expected.DisplayOrder);
        actual.UpvoteCount.Should().Be(expected.UpvoteCount);
        actual.CreatedAt.Should().BeCloseTo(expected.CreatedAt, TimeSpan.FromMilliseconds(1));
        actual.UpdatedAt.Should().Be(expected.UpdatedAt);
    }
}
