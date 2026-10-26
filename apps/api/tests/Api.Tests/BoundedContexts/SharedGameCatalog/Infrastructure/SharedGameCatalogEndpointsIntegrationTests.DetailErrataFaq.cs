using System.Net;
using System.Text.Json;
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
/// #4565 — <c>GET /api/v1/shared-games/{id}</c> riporta le errata e le FAQ del gioco, e le riporta
/// aggiornate dopo ogni scrittura.
/// </summary>
/// <remarks>
/// <para>
/// Due difetti, e i test li separano. (1) <c>GetSharedGameByIdQueryHandler</c> costruiva i due campi da
/// <c>game.Erratas</c> e <c>game.Faqs</c> dell'aggregato letto con <c>GetByIdAsync</c>, che non li
/// include: <c>erratas: []</c> e <c>faqs: []</c> sempre. I test «Detail_Lists…» falliscono solo per
/// questo. (2) Il dettaglio sta in cache sotto il tag <c>shared-game:{id}</c> (durata:
/// <c>DetailCacheL1Expiration</c> e <c>DetailCacheL2Expiration</c> di <c>GetSharedGameByIdQueryHandler</c>)
/// e gli handler di errata e FAQ non lo invalidavano: corretta la lettura, una scrittura si sarebbe
/// vista alla scadenza. I test «Detail_After…» leggono il dettaglio una volta per riempire la cache,
/// scrivono, e lo rileggono.
/// </para>
/// <para>
/// 🔴 Perché la cache del test è quella vera. L'host di integrazione sostituisce
/// <c>IHybridCacheService</c> con un doppio che non fa niente (<c>TestHybridCacheService</c>), ma
/// <c>HybridCache</c> di Microsoft, quello con cui il dettaglio è scritto, resta il reale (L1 in
/// memoria). Quindi la rilettura è davvero servita dalla cache. Qui l'evizione tramite il wrapper non fa
/// niente (è il doppio) e non c'è Redis, quindi nemmeno il Pub/Sub: il test diventa verde solo se
/// l'handler evince il tag col <c>HybridCache</c> nativo. È il passo che in produzione serve per la lettura
/// subito dopo la scrittura sulla replica che ha eseguito il comando e per l'esecuzione senza Redis: con
/// Redis il wrapper raggiunge la voce via Pub/Sub, ma in modo asincrono (vedi
/// <c>SharedGameCacheInvalidator</c>, #4561).
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    private sealed record DetailErrata(
        Guid Id, string Description, string PageReference, DateTime PublishedDate);

    private sealed record DetailFaq(
        Guid Id, Guid GameId, string Question, string Answer, int Order, int Upvotes);

    private sealed record DetailErrataAndFaqs(List<DetailErrata> Erratas, List<DetailFaq> Faqs);

    // ────────────────────────────────────────────────────────────────────────────
    // Lettura: il dettaglio riporta le righe che il gioco ha già
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_ListsTheErrataOfTheGame_NewestPublishedFirst()
    {
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var older = await SeedDetailErrataAsync(game.Id, "Più vecchia", "p. 3", new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        var newer = await SeedDetailErrataAsync(game.Id, "Più recente", "p. 9", new DateTime(2025, 8, 20, 0, 0, 0, DateTimeKind.Utc));
        await SeedDetailErrataAsync(otherGame.Id, "Di un altro gioco", "p. 1", ErrataPublished);

        var detail = await ReadDetailAsync(game.Id);

        detail.Erratas.Select(e => e.Id).Should().Equal(new[] { newer.Id, older.Id },
            "the detail lists the game's errata, newest PublishedDate first, and none of another game's");
        var first = detail.Erratas[0];
        first.Description.Should().Be("Più recente");
        first.PageReference.Should().Be("p. 9");
        first.PublishedDate.Should().Be(newer.PublishedDate);
    }

    [Fact]
    public async Task Detail_ListsTheFaqsOfTheGame_InDisplayOrder()
    {
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);
        var second = await SeedFaqAsync(game.Id, "Seconda", "Risposta due", displayOrder: 2, upvotes: 7);
        var first = await SeedFaqAsync(game.Id, "Prima", "Risposta uno", displayOrder: 1, upvotes: 0);
        await SeedFaqAsync(otherGame.Id, "Di un altro gioco", "x", displayOrder: 0, upvotes: 0);

        var detail = await ReadDetailAsync(game.Id);

        detail.Faqs.Select(f => f.Id).Should().Equal(new[] { first.Id, second.Id },
            "the detail lists the game's FAQs by DisplayOrder, and none of another game's");
        detail.Faqs[1].Should().BeEquivalentTo(new DetailFaq(
            second.Id, game.Id, "Seconda", "Risposta due", Order: 2, Upvotes: 7));
    }

    /// <summary>
    /// Gli spareggi dell'ordine (#4565): FAQ con lo stesso <c>DisplayOrder</c> per voti decrescenti e poi
    /// per <c>CreatedAt</c>, errata con la stessa <c>PublishedDate</c> per <c>CreatedAt</c> decrescente.
    /// </summary>
    /// <remarks>
    /// Le righe si inseriscono in ordine OPPOSTO a quello atteso: a parità di chiave Postgres restituisce
    /// di norma l'ordine di inserimento, quindi senza lo spareggio il test vede l'ordine di inserimento e
    /// fallisce. Con le righe già in ordine il test passerebbe anche senza spareggio.
    /// </remarks>
    [Fact]
    public async Task Detail_TiedRows_AreOrderedByTheTieBreakers()
    {
        var game = await CreateTestGameAsync(GameStatus.Published);
        var t0 = new DateTime(2025, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        // FAQ, tutte con DisplayOrder 1 salvo una con 0 inserita per ultima. Ordine atteso:
        // prima = DisplayOrder 0; poi voti decrescenti (5, 5, 1); i due da 5 per CreatedAt crescente.
        var fewVotes = await SeedDetailFaqAsync(game.Id, "Pochi voti", displayOrder: 1, upvotes: 1, createdAt: t0);
        var tiedNewer = await SeedDetailFaqAsync(game.Id, "5 voti, più recente", displayOrder: 1, upvotes: 5, createdAt: t0.AddMinutes(10));
        var tiedOlder = await SeedDetailFaqAsync(game.Id, "5 voti, più vecchia", displayOrder: 1, upvotes: 5, createdAt: t0.AddMinutes(5));
        var firstByOrder = await SeedDetailFaqAsync(game.Id, "DisplayOrder 0", displayOrder: 0, upvotes: 0, createdAt: t0.AddMinutes(20));

        // Errata, tutte con la stessa PublishedDate salvo una più recente inserita per prima.
        // Ordine atteso: la più recente per PublishedDate; poi CreatedAt decrescente.
        var published = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var newestPublished = await SeedDetailErrataAsync(game.Id, "Pubblicata dopo", "p. 1", published.AddDays(30), createdAt: t0);
        var createdFirst = await SeedDetailErrataAsync(game.Id, "Creata prima", "p. 2", published, createdAt: t0);
        var createdLast = await SeedDetailErrataAsync(game.Id, "Creata dopo", "p. 3", published, createdAt: t0.AddMinutes(10));
        var createdMiddle = await SeedDetailErrataAsync(game.Id, "Creata in mezzo", "p. 4", published, createdAt: t0.AddMinutes(5));

        var detail = await ReadDetailAsync(game.Id);

        detail.Faqs.Select(f => f.Id).Should().Equal(
            new[] { firstByOrder.Id, tiedOlder.Id, tiedNewer.Id, fewVotes.Id },
            "same DisplayOrder: more votes first, then the older FAQ first");
        detail.Erratas.Select(e => e.Id).Should().Equal(
            new[] { newestPublished.Id, createdLast.Id, createdMiddle.Id, createdFirst.Id },
            "same PublishedDate: the most recently created erratum first");
    }

    [Fact]
    public async Task Detail_OfASoftDeletedGame_Returns404_EvenIfItHasErrataAndFaqs()
    {
        // La proiezione non deve far riemergere nulla: il gioco cancellato resta 404.
        var game = await CreateTestGameAsync(GameStatus.Published);
        await SeedFaqAsync(game.Id, "Domanda", "Risposta", displayOrder: 1, upvotes: 1);
        await SeedDetailErrataAsync(game.Id, "Errata", "p. 1", ErrataPublished);
        await SoftDeleteGameAsync(game.Id);

        using var response = await _client.GetAsync(
            $"/api/v1/shared-games/{game.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Scrittura → rilettura: il dettaglio in cache segue le errata
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_AfterAddingAnErratum_ReportsIt()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        (await ReadDetailAsync(game.Id)).Erratas.Should().BeEmpty("first read: fills the cache");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/errata",
            token,
            new { description = "Il mercante pesca due carte", pageReference = "p. 7", publishedDate = ErrataPublished });
        using var post = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        post.StatusCode.Should().Be(HttpStatusCode.Created, await post.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var detail = await ReadDetailAsync(game.Id);

        detail.Erratas.Should().ContainSingle("the cached detail must be evicted by the write")
            .Which.Description.Should().Be("Il mercante pesca due carte");
    }

    [Fact]
    public async Task Detail_AfterUpdatingAnErratum_ReportsTheNewText()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedDetailErrataAsync(game.Id, "Testo originale", "p. 3", ErrataPublished);

        (await ReadDetailAsync(game.Id)).Erratas.Single().Description.Should().Be("Testo originale");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/errata/{target.Id}",
            token,
            new { description = "Testo corretto", pageReference = "p. 4", publishedDate = ErrataPublished });
        using var put = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var errata = (await ReadDetailAsync(game.Id)).Erratas.Should().ContainSingle().Subject;
        errata.Description.Should().Be("Testo corretto");
        errata.PageReference.Should().Be("p. 4");
    }

    [Fact]
    public async Task Detail_AfterDeletingAnErratum_NoLongerReportsIt()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedDetailErrataAsync(game.Id, "Da togliere", "p. 1", ErrataPublished);
        var other = await SeedDetailErrataAsync(game.Id, "Da tenere", "p. 2", ErrataPublished.AddDays(-1));

        (await ReadDetailAsync(game.Id)).Erratas.Should().HaveCount(2);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/admin/shared-games/{game.Id}/errata/{target.Id}",
            token);
        using var delete = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent, await delete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        (await ReadDetailAsync(game.Id)).Erratas.Select(e => e.Id).Should().Equal(new[] { other.Id });
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Scrittura → rilettura: il dettaglio in cache segue le FAQ
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_AfterAddingAFaq_ReportsIt()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        (await ReadDetailAsync(game.Id)).Faqs.Should().BeEmpty("first read: fills the cache");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/faq",
            token,
            new { question = "Si può commerciare col banco?", answer = "Sì, 4 a 1.", order = 2 });
        using var post = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        post.StatusCode.Should().Be(HttpStatusCode.Created, await post.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var faq = (await ReadDetailAsync(game.Id)).Faqs.Should()
            .ContainSingle("the cached detail must be evicted by the write").Subject;
        faq.Question.Should().Be("Si può commerciare col banco?");
        faq.Order.Should().Be(2);
    }

    [Fact]
    public async Task Detail_AfterUpdatingAFaq_ReportsTheNewText()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedFaqAsync(game.Id, "Domanda originale", "Risposta originale", displayOrder: 1, upvotes: 5);

        (await ReadDetailAsync(game.Id)).Faqs.Single().Question.Should().Be("Domanda originale");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/faq/{target.Id}",
            token,
            new { question = "Domanda corretta", answer = "Risposta corretta", order = 7 });
        using var put = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        put.StatusCode.Should().Be(HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var faq = (await ReadDetailAsync(game.Id)).Faqs.Should().ContainSingle().Subject;
        faq.Question.Should().Be("Domanda corretta");
        faq.Answer.Should().Be("Risposta corretta");
        faq.Order.Should().Be(7);
        faq.Upvotes.Should().Be(5, "editing the text keeps the votes");
    }

    [Fact]
    public async Task Detail_AfterDeletingAFaq_NoLongerReportsIt()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedFaqAsync(game.Id, "Da togliere", "x", displayOrder: 1, upvotes: 0);
        var other = await SeedFaqAsync(game.Id, "Da tenere", "y", displayOrder: 2, upvotes: 0);

        (await ReadDetailAsync(game.Id)).Faqs.Should().HaveCount(2);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/admin/shared-games/{game.Id}/faq/{target.Id}",
            token);
        using var delete = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent, await delete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        (await ReadDetailAsync(game.Id)).Faqs.Select(f => f.Id).Should().Equal(new[] { other.Id });
    }

    [Fact]
    public async Task Detail_AfterAnUpvote_ReportsTheNewCount()
    {
        // Il voto è un UPDATE immediato sulla riga della FAQ (ExecuteUpdate), non passa dallo unit of
        // work: l'evizione non può appoggiarsi a un SaveChanges.
        var game = await CreateTestGameAsync(GameStatus.Published);
        var faq = await SeedFaqAsync(game.Id, "Domanda votata", "Risposta votata", displayOrder: 1, upvotes: 3);

        (await ReadDetailAsync(game.Id)).Faqs.Single().Upvotes.Should().Be(3);

        using var vote = await _client.PostAsync(
            $"/api/v1/faqs/{faq.Id}/upvote", content: null, TestContext.Current.CancellationToken);
        vote.StatusCode.Should().Be(HttpStatusCode.OK, await vote.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        (await ReadDetailAsync(game.Id)).Faqs.Single().Upvotes.Should().Be(4);
    }

    [Fact]
    public async Task Detail_OfAnotherGame_StaysCached_WhenAnErratumIsAdded()
    {
        // L'evizione è per gioco (tag `shared-game:{id}`), non un flush del catalogo: il dettaglio di un
        // altro gioco resta in cache. Si vede perché una riga scritta SENZA passare dall'handler (e quindi
        // senza evizione) non compare nella sua rilettura.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var otherGame = await CreateTestGameAsync(GameStatus.Published);

        (await ReadDetailAsync(otherGame.Id)).Erratas.Should().BeEmpty("fills the cache of the other game");
        await SeedDetailErrataAsync(otherGame.Id, "Scritta sotto la cache", "p. 1", ErrataPublished);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/errata",
            token,
            new { description = "Per il primo gioco", pageReference = "p. 2", publishedDate = ErrataPublished });
        using var post = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        (await ReadDetailAsync(otherGame.Id)).Erratas.Should().BeEmpty(
            "the write on one game must not flush the cached detail of another");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Helper
    // ────────────────────────────────────────────────────────────────────────────

    private async Task<DetailErrataAndFaqs> ReadDetailAsync(Guid gameId)
    {
        using var response = await _client.GetAsync(
            $"/api/v1/shared-games/{gameId}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var erratas = document.RootElement.GetProperty("erratas")
            .Deserialize<List<DetailErrata>>(JsonSerializerOptions.Web)!;
        var faqs = document.RootElement.GetProperty("faqs")
            .Deserialize<List<DetailFaq>>(JsonSerializerOptions.Web)!;
        return new DetailErrataAndFaqs(erratas, faqs);
    }

    private async Task<GameErrataEntity> SeedDetailErrataAsync(
        Guid gameId, string description, string pageReference, DateTime publishedDate, DateTime? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var row = new GameErrataEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Description = description,
            PageReference = pageReference,
            PublishedDate = publishedDate,
            CreatedAt = createdAt ?? new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };
        db.GameErrata.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row;
    }

    private async Task<GameFaqEntity> SeedDetailFaqAsync(
        Guid gameId, string question, int displayOrder, int upvotes, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var row = new GameFaqEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Question = question,
            Answer = "Risposta",
            DisplayOrder = displayOrder,
            UpvoteCount = upvotes,
            CreatedAt = createdAt,
        };
        db.GameFaqs.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row;
    }

    private async Task SoftDeleteGameAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.SharedGames
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(g => g.IsDeleted, true),
                TestContext.Current.CancellationToken);
    }
}
