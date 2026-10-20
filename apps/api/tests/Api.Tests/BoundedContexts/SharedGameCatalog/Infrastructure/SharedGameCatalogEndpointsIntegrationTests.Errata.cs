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
/// #4543 — le errata di un gioco del catalogo, via HTTP e verificate sul database.
/// </summary>
/// <remarks>
/// <para>
/// Prima della correzione nessuna delle tre operazioni funzionava: il POST rispondeva 201 senza
/// scrivere la riga, perché <c>SharedGameRepository</c> non mappava le errata; PUT e DELETE
/// rispondevano 400 prima dell'handler, perché il parametro di rotta <c>{id}</c> si chiamava
/// <c>_</c> nel metodo e il binding lo cercava nella query; e, forzato il binding, rispondevano
/// 404 perché la lettura non caricava le errata nell'aggregato.
/// </para>
/// <para>
/// I codici HTTP da soli non bastano: il POST rispondeva già 201. Ogni test asserisce la riga in
/// <c>game_errata</c>. Gli ultimi tre fissano il rischio della correzione: l'update di uno
/// <c>SharedGame</c> riguarda tutto l'aggregato, e un comando che non carica le errata non deve
/// cancellarle.
/// </para>
/// <para>
/// Classe parziale per riusare l'host della classe (#4050): una classe nuova ne costruirebbe un
/// altro, circa 25 secondi.
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    private static readonly DateTime ErrataPublished = new(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AddErrata_WritesTheRow()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/errata",
            token,
            new { description = "Il mercante pesca due carte", pageReference = "p. 7", publishedDate = ErrataPublished });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        var errataId = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);

        var rows = await ErrataRowsAsync(game.Id);
        rows.Should().ContainSingle("the 201 must correspond to a row in game_errata, not just an id");
        var row = rows[0];
        row.Id.Should().Be(errataId);
        row.Description.Should().Be("Il mercante pesca due carte");
        row.PageReference.Should().Be("p. 7");
        row.PublishedDate.Should().Be(ErrataPublished);
    }

    [Fact]
    public async Task UpdateErrata_ChangesTheRow_AndLeavesTheOthersAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedErrataAsync(game.Id, "Testo originale", "p. 3");
        var other = await SeedErrataAsync(game.Id, "Un'altra errata", "p. 9");

        var newDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/errata/{target.Id}",
            token,
            new { description = "Testo corretto", pageReference = "p. 4", publishedDate = newDate });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = (await ErrataRowsAsync(game.Id)).ToDictionary(r => r.Id);
        rows.Should().HaveCount(2);

        var updated = rows[target.Id];
        updated.Description.Should().Be("Testo corretto");
        updated.PageReference.Should().Be("p. 4");
        updated.PublishedDate.Should().Be(newDate);
        updated.CreatedAt.Should().BeCloseTo(target.CreatedAt, TimeSpan.FromMilliseconds(1),
            "an update keeps the creation timestamp");

        AssertUnchanged(rows[other.Id], other);
    }

    [Fact]
    public async Task DeleteErrata_RemovesTheRow_AndLeavesTheOthersAlone()
    {
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var target = await SeedErrataAsync(game.Id, "Da togliere", "p. 1");
        var other = await SeedErrataAsync(game.Id, "Da tenere", "p. 2");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete,
            $"/api/v1/admin/shared-games/{game.Id}/errata/{target.Id}",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = await ErrataRowsAsync(game.Id);
        rows.Should().ContainSingle();
        AssertUnchanged(rows[0], other);
    }

    [Fact]
    public async Task UpdateErrata_OnAnErrataThatDoesNotExist_Returns404_Not400()
    {
        // Il 400 era il binding che cercava `_` nella query: un 404 qui prova che la richiesta
        // arriva all'handler.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/errata/{Guid.NewGuid()}",
            token,
            new { description = "x", pageReference = "p. 1", publishedDate = ErrataPublished });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddErrata_KeepsTheErrataAlreadySaved()
    {
        // Il POST legge il gioco con GetByIdAsync, che non carica le errata: quelle salvate non
        // sono nell'aggregato e l'update non deve trattarle come tolte.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);
        var existing = await SeedErrataAsync(game.Id, "Già salvata", "p. 5");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/errata",
            token,
            new { description = "Nuova", pageReference = "p. 6", publishedDate = ErrataPublished });
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var rows = (await ErrataRowsAsync(game.Id)).ToDictionary(r => r.Id);
        rows.Should().HaveCount(2);
        AssertUnchanged(rows[existing.Id], existing);
    }

    [Fact]
    public async Task AnotherUpdateOfTheGame_KeepsItsErrata()
    {
        // Un comando qualsiasi che legge e salva lo SharedGame senza toccare le errata:
        // submit-for-approval passa da GetByIdAsync + Update.
        var token = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);
        var existing = await SeedErrataAsync(game.Id, "Deve restare", "p. 8");

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval",
            token);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, body);

        var rows = await ErrataRowsAsync(game.Id);
        rows.Should().ContainSingle();
        AssertUnchanged(rows[0], existing);
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(db);
        return token;
    }

    private async Task<GameErrataEntity> SeedErrataAsync(Guid gameId, string description, string pageReference)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var row = new GameErrataEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            Description = description,
            PageReference = pageReference,
            PublishedDate = ErrataPublished,
            CreatedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };
        db.GameErrata.Add(row);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return row;
    }

    private async Task<List<GameErrataEntity>> ErrataRowsAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.GameErrata
            .AsNoTracking()
            .Where(e => e.SharedGameId == gameId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertUnchanged(GameErrataEntity actual, GameErrataEntity expected)
    {
        actual.Description.Should().Be(expected.Description);
        actual.PageReference.Should().Be(expected.PageReference);
        actual.PublishedDate.Should().Be(expected.PublishedDate);
        actual.CreatedAt.Should().BeCloseTo(expected.CreatedAt, TimeSpan.FromMilliseconds(1));
    }
}
