using System.Net;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// #4603 — quando lo stato di un gioco cambia (pubblicato, archiviato, cancellato, rifiutato), il
/// dettaglio e la lista del catalogo in cache lo seguono.
/// </summary>
/// <remarks>
/// <para>
/// Il difetto. <c>GET /api/v1/shared-games/{id}</c> sta in cache sotto il tag <c>shared-game:{id}</c> e la
/// lista (<c>GET /api/v1/shared-games</c>) sotto <c>search-games</c>. L'endpoint pubblico nasconde i giochi
/// che non sono <c>Published</c> <b>dopo</b> la cache, cioè sul DTO già in cache: se l'handler che cambia lo
/// stato non evince la voce, il pubblico vede lo stato di prima finché la voce non scade. Due direzioni:
/// un gioco <b>archiviato o cancellato</b> resta visibile; un gioco <b>pubblicato</b> dopo che un admin ne
/// ha letto la bozza resta 404.
/// </para>
/// <para>
/// Come si legge ogni test. Una prima lettura riempie la cache; il comando HTTP cambia lo stato; la
/// seconda lettura deve vedere il nuovo stato. Il test «NeverRead» è il controllo: lo stesso passaggio
/// senza la prima lettura. Se il controllo è verde e gli altri sono rossi, a fare la differenza è
/// la cache e non l'endpoint.
/// </para>
/// <para>
/// 🔴 La cache che conta è <c>HybridCache</c> nativo. L'host di integrazione sostituisce
/// <c>IHybridCacheService</c> con un doppio che non fa niente, quindi l'evizione tramite il wrapper non
/// si vede qui, e senza Redis non c'è nemmeno il Pub/Sub: un test verde significa che il percorso ha
/// evinto il tag col <c>HybridCache</c> nativo (vedi <c>SharedGameCacheInvalidator</c>, #4561).
/// L'host è condiviso fra i test della classe e la cache con lui: il dettaglio è per id di gioco (uno
/// per test), la lista ha chiave dai parametri, e per questo i test di lista usano un termine di ricerca
/// proprio.
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    private sealed record DetailRead(HttpStatusCode Status, string? GameStatus, string? Title);

    // ────────────────────────────────────────────────────────────────────────────
    // Gioco che esce dal catalogo: il pubblico non lo deve più vedere
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_ArchivedAfterAPublicRead_IsHiddenFromThePublic()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(HttpStatusCode.OK, "first read: fills the cache");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/archive", HttpStatusCode.NoContent);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(
            HttpStatusCode.NotFound, "an archived game must not be served to the public from the cached detail");
    }

    [Fact]
    public async Task Detail_DeletedAfterAPublicRead_IsHiddenFromThePublic()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(HttpStatusCode.OK, "first read: fills the cache");

        await SendAdminAsync(admin, HttpMethod.Delete, $"/api/v1/admin/shared-games/{game.Id}", HttpStatusCode.NoContent);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(
            HttpStatusCode.NotFound, "a deleted game must not be served to the public from the cached detail");
    }

    [Fact]
    public async Task Detail_DeleteRequestApprovedAfterAPublicRead_IsHiddenFromThePublic()
    {
        var admin = await CreateAdminTokenAsync();
        var editor = await CreateEditorTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(HttpStatusCode.OK, "first read: fills the cache");

        // L'editor non cancella: apre una richiesta, e il gioco resta com'è finché l'admin non approva.
        using var requestMessage = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/admin/shared-games/{game.Id}", editor);
        using var requested = await NewCookielessClient().SendAsync(requestMessage, TestContext.Current.CancellationToken);
        requested.StatusCode.Should().Be(HttpStatusCode.Accepted, await requested.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var requestId = JsonSerializer.Deserialize<Guid>(
            await requested.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(
            HttpStatusCode.OK, "a delete REQUEST changes nothing: the game is still published");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/approve-delete/{requestId}", HttpStatusCode.NoContent);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(
            HttpStatusCode.NotFound, "the approved delete removes the game: the cached detail must not outlive it");
    }

    [Fact]
    public async Task Detail_DraftRemovedByExcelConfirmAfterAnAdminRead_IsGone()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);

        (await ReadDetailAsStatusAsync(game.Id, admin)).Status.Should().Be(HttpStatusCode.OK, "first read (admin sees drafts): fills the cache");

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/admin/catalog-ingestion/excel-confirm",
            HttpStatusCode.OK,
            new { newGames = Array.Empty<object>(), modifiedGames = Array.Empty<object>(), removedGameIds = new[] { game.Id } });

        (await ReadDetailAsStatusAsync(game.Id, admin)).Status.Should().Be(
            HttpStatusCode.NotFound, "the soft-deleted draft must not be served to the admin from the cached detail");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Gioco che entra nel catalogo: il pubblico lo deve vedere subito
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_ApprovedAfterAnAdminRead_IsVisibleToThePublic()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.PendingApproval), "first read: fills the cache");
        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(HttpStatusCode.NotFound, "the public does not see a pending game");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/approve-publication", HttpStatusCode.NoContent);

        var afterApproval = await ReadDetailAsStatusAsync(game.Id, token: null);
        afterApproval.Status.Should().Be(HttpStatusCode.OK, "a published game must be served to the public");
        afterApproval.GameStatus.Should().Be(nameof(GameStatus.Published));
    }

    [Fact]
    public async Task Detail_QuickPublishedAfterAnAdminRead_IsVisibleToThePublic()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.Draft), "first read: fills the cache");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/quick-publish", HttpStatusCode.NoContent);

        var afterPublish = await ReadDetailAsStatusAsync(game.Id, token: null);
        afterPublish.Status.Should().Be(HttpStatusCode.OK, "a published game must be served to the public");
        afterPublish.GameStatus.Should().Be(nameof(GameStatus.Published));
    }

    [Fact]
    public async Task Detail_BatchApprovedAfterAnAdminRead_IsVisibleToThePublic()
    {
        var admin = await CreateAdminTokenAsync();
        var first = await CreateTestGameAsync(GameStatus.PendingApproval);
        var second = await CreateTestGameAsync(GameStatus.PendingApproval);

        (await ReadDetailAsStatusAsync(first.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.PendingApproval), "first read: fills the cache");
        (await ReadDetailAsStatusAsync(second.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.PendingApproval), "first read: fills the cache");

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/admin/shared-games/batch-approve",
            HttpStatusCode.OK,
            new { gameIds = new[] { first.Id, second.Id }, note = (string?)null });

        (await ReadDetailAsStatusAsync(first.Id, token: null)).Status.Should().Be(HttpStatusCode.OK, "every game of the batch is published");
        (await ReadDetailAsStatusAsync(second.Id, token: null)).Status.Should().Be(HttpStatusCode.OK, "every game of the batch is published");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Gioco che cambia stato dentro il flusso di approvazione: l'admin vede lo stato nuovo
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_SubmittedForApprovalAfterAnAdminRead_ReportsThePendingStatus()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.Draft), "first read: fills the cache");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval", HttpStatusCode.NoContent);

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.PendingApproval));
    }

    [Fact]
    public async Task Detail_RejectedAfterAnAdminRead_ReportsDraft()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.PendingApproval), "first read: fills the cache");

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/reject-publication",
            HttpStatusCode.NoContent,
            new { reason = "Mancano le immagini" });

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.Draft));
    }

    [Fact]
    public async Task Detail_BatchRejectedAfterAnAdminRead_ReportsDraft()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.PendingApproval), "first read: fills the cache");

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            "/api/v1/admin/shared-games/batch-reject",
            HttpStatusCode.OK,
            new { gameIds = new[] { game.Id }, reason = "Mancano le immagini" });

        (await ReadDetailAsStatusAsync(game.Id, admin)).GameStatus.Should().Be(nameof(GameStatus.Draft));
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Controlli
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_ArchivedWithoutAPriorRead_Returns404ForThePublic()
    {
        // Il controllo dei test sopra: stesso comando, ma la cache è vuota. Se è verde, l'endpoint nasconde
        // già i giochi archiviati e a tenere visibile quello dei test rossi è solo la voce in cache.
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/archive", HttpStatusCode.NoContent);

        (await ReadDetailAsStatusAsync(game.Id, token: null)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detail_OfAnotherGame_StaysCached_WhenAGameIsArchived()
    {
        // L'evizione è per gioco, non un flush del catalogo. Si vede perché il titolo dell'altro gioco,
        // cambiato SOTTO la cache (senza passare da un handler, quindi senza evizione), non compare nella
        // sua rilettura.
        var admin = await CreateAdminTokenAsync();
        var archived = await CreateTestGameAsync(GameStatus.Published);
        var other = await CreateTestGameAsync(GameStatus.Published);

        var firstRead = await ReadDetailAsStatusAsync(other.Id, token: null);
        firstRead.Status.Should().Be(HttpStatusCode.OK, "fills the cache of the other game");
        await RenameGameBeneathTheCacheAsync(other.Id, "Titolo scritto sotto la cache");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{archived.Id}/archive", HttpStatusCode.NoContent);

        (await ReadDetailAsStatusAsync(other.Id, token: null)).Title.Should().Be(
            firstRead.Title, "archiving one game must not flush the cached detail of another");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Lista (`search-games`)
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ArchivedAfterAPublicRead_DisappearsFromThePublicList()
    {
        var admin = await CreateAdminTokenAsync();
        var word = UniqueSearchWord();
        var game = await SeedGameWithTitleAsync(GameStatus.Published, $"{word} Quest");

        (await ListPublicIdsAsync(word)).Should().Contain(game.Id, "first read: fills the list cache");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/archive", HttpStatusCode.NoContent);

        (await ListPublicIdsAsync(word)).Should().NotContain(game.Id, "an archived game must leave the public list");
    }

    [Fact]
    public async Task List_QuickPublishedAfterAPublicRead_AppearsInThePublicList()
    {
        var admin = await CreateAdminTokenAsync();
        var word = UniqueSearchWord();
        var game = await SeedGameWithTitleAsync(GameStatus.Draft, $"{word} Quest");

        (await ListPublicIdsAsync(word)).Should().NotContain(game.Id, "first read: fills the list cache without the draft");

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/quick-publish", HttpStatusCode.NoContent);

        (await ListPublicIdsAsync(word)).Should().Contain(game.Id, "a published game must enter the public list");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Helper
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un client senza cookie: la lettura «pubblica» non deve ereditare una sessione da una risposta
    /// precedente, e quella dell'admin porta il cookie esplicito.
    /// </summary>
    private HttpClient NewCookielessClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private async Task<DetailRead> ReadDetailAsStatusAsync(Guid gameId, string? token)
    {
        using var request = token is null
            ? new HttpRequestMessage(HttpMethod.Get, $"/api/v1/shared-games/{gameId}")
            : TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/shared-games/{gameId}", token);
        using var response = await NewCookielessClient().SendAsync(request, TestContext.Current.CancellationToken);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new DetailRead(response.StatusCode, null, null);
        }

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var status = document.RootElement.GetProperty("status");
        var statusName = status.ValueKind == JsonValueKind.Number
            ? ((GameStatus)status.GetInt32()).ToString()
            : status.GetString();
        return new DetailRead(response.StatusCode, statusName, document.RootElement.GetProperty("title").GetString());
    }

    private async Task SendAdminAsync(
        string token, HttpMethod method, string url, HttpStatusCode expected, object? body = null)
    {
        using var request = body is null
            ? TestSessionHelper.CreateAuthenticatedRequest(method, url, token)
            : TestSessionHelper.CreateAuthenticatedRequest(method, url, token, body);
        using var response = await NewCookielessClient().SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<string> CreateEditorTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateEditorSessionAsync(db);
        return token;
    }

    private async Task<List<Guid>> ListPublicIdsAsync(string searchTerm)
    {
        using var response = await NewCookielessClient().GetAsync(
            $"/api/v1/shared-games?search={searchTerm}&pageNumber=1&pageSize=50", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
    }

    /// <summary>
    /// Una parola solo di lettere, diversa a ogni chiamata: la ricerca è full-text con configurazione
    /// italiana, e un GUID in esadecimale non è un lessema affidabile. Diversa a ogni test anche perché la
    /// chiave della lista in cache è fatta dai parametri e l'host (con la sua cache) è della classe.
    /// </summary>
    private static string UniqueSearchWord() =>
        "Zq" + new string(Guid.NewGuid().ToByteArray().Take(10).Select(b => (char)('a' + (b % 26))).ToArray());

    private async Task<SharedGameEntity> SeedGameWithTitleAsync(GameStatus status, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = 2024,
            Description = "Test description",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)status,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.SharedGames.Add(game);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return game;
    }

    private async Task RenameGameBeneathTheCacheAsync(Guid gameId, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.SharedGames
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(g => g.Title, title),
                TestContext.Current.CancellationToken);
    }
}
