using System.Net;
using System.Text.Json;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.Integration.KnowledgeBase;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// La fixture di <see cref="DeletePdfKbCacheIntegrationTests"/>: una cache ibrida che ricorda davvero. Gli
/// host di integrazione ne montano una che richiama sempre la fabbrica, e con quella «leggi, cancella,
/// rileggi» passa qualunque cosa faccia il codice (vedi <see cref="RememberingHybridCache"/>).
/// </summary>
public sealed class DeletePdfKbCacheHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "pdf_delete_cache")
{
    internal RememberingHybridCache HybridCache { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHybridCacheService>();
            services.AddSingleton<IHybridCacheService>(HybridCache);
        }));
}

/// <summary>
/// #4308: cancellato un PDF (<c>DELETE /api/v1/pdf/{pdfId}</c>), la riga sparisce dal database ma
/// <c>GET /api/v1/kb-docs</c> («I miei documenti») continua a elencarlo, e il documento e i suoi passaggi
/// restano leggibili, finché non scade la cache. <c>DeletePdfCommandHandler</c> svuotava la cache delle
/// risposte AI e quella semantica, non quella ibrida.
/// </summary>
/// <remarks>
/// Su Postgres, via HTTP. Il proprietario ha un secondo PDF che fa da controllo: la cancellazione di uno non
/// deve portarsi via l'altro, e un elenco vuoto non proverebbe che l'elenco è stato riletto.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4308")]
public sealed class DeletePdfKbCacheIntegrationTests
    : IClassFixture<DeletePdfKbCacheHostFixture>, IAsyncLifetime
{
    private readonly DeletePdfKbCacheHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private Guid _otherGameId;
    private Guid _pdfId;
    private Guid _controlPdfId;

    public DeletePdfKbCacheIntegrationTests(DeletePdfKbCacheHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        // La cache vive quanto la classe: ogni test parte da vuota.
        _hostFixture.HybridCache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);

        _gameId = await SeedPrivateGameAsync(db, "Il mio prototipo");
        _otherGameId = await SeedPrivateGameAsync(db, "Un altro prototipo");
        _pdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, _gameId, "Ready", Ct,
            ("At setup each player draws five cards.", QueryVectorEmbeddings.Axis(1f, 0f)));
        _controlPdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, _otherGameId, "Ready", Ct,
            ("In the other prototype each player draws seven tiles.", QueryVectorEmbeddings.Axis(0f, 1f)));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// «I miei documenti» tiene l'elenco in cache per cinque minuti. Chi l'ha guardato prima di cancellare il
    /// PDF non deve ritrovarci un documento che non esiste più.
    /// </summary>
    [Fact]
    public async Task DeletingAPdf_TakesItOutOfTheOwnersCachedList()
    {
        // La prima lettura mette l'elenco in cache: è il caso che conta.
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _pdfId, _controlPdfId });

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("the row is gone: the list must not keep showing it");
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _controlPdfId });
    }

    /// <summary>
    /// Il documento e i suoi passaggi stanno in cache anche loro (<c>kbDoc:</c>, <c>kbChunks:</c>), i passaggi
    /// per mezz'ora e col testo dentro. Chi li aveva aperti non deve poterli rileggere.
    /// </summary>
    [Fact]
    public async Task ADocumentReadBefore_CannotBeReadAfterItsPdfIsDeleted()
    {
        (await GetAsync($"/api/v1/kb-docs/{_pdfId}")).Should().Be(HttpStatusCode.OK);
        (await GetAsync($"/api/v1/kb-docs/{_pdfId}/chunks")).Should().Be(HttpStatusCode.OK);

        (await DeletePdfAsync(_pdfId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetAsync($"/api/v1/kb-docs/{_pdfId}")).Should().Be(HttpStatusCode.NotFound);
        (await GetAsync($"/api/v1/kb-docs/{_pdfId}/chunks")).Should().Be(HttpStatusCode.NotFound);
        // Il PDF di controllo non c'entra.
        (await GetAsync($"/api/v1/kb-docs/{_controlPdfId}")).Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Se un altro gioco usa ancora il PDF, il gestore toglie un collegamento e la riga resta (#2949): l'elenco
    /// non cambia, quindi non c'è niente da invalidare.
    /// </summary>
    [Fact]
    public async Task APdfAnotherGameStillLinks_IsOnlyUnlinked_AndItsCachesAreLeftAlone()
    {
        await LinkGameToPdfAsync(_gameId, _pdfId);
        await LinkGameToPdfAsync(_otherGameId, _pdfId);
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _pdfId, _controlPdfId });

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeTrue("another game still uses it: the row survives");
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _pdfId, _controlPdfId });
        // Le due etichette del documento sono solo sue: nessuno le ha toccate.
        _hostFixture.HybridCache.RemovalsOf($"kbDoc:{_pdfId:N}").Should().Be(0);
        _hostFixture.HybridCache.RemovalsOf($"kbChunks:{_pdfId:N}").Should().Be(0);
    }

    /// <summary>
    /// Una cache che non si svuota vuol dire dati vecchi per qualche minuto, non una cancellazione fallita.
    /// </summary>
    [Fact]
    public async Task AnInvalidationThatFails_DoesNotFailTheDeletion()
    {
        _hostFixture.HybridCache.WhenRemoving(
            $"user:{_userId}",
            () => Task.FromException(new InvalidOperationException("La cache è giù: lo fa il test.")));

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse();
        // Senza questa riga il test passerebbe anche se l'invalidazione non venisse mai chiesta.
        _hostFixture.HybridCache.RemovalsOf($"user:{_userId}").Should().BeGreaterThan(0,
            "the deletion must have tried to clear the owner's list, and survived its failure");
    }

    // ─── Supporto ────────────────────────────────────────────────

    private async Task<Guid> SeedPrivateGameAsync(MeepleAiDbContext db, string title)
    {
        var gameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = _userId,
            Title = title,
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }

    private Task<HttpResponseMessage> DeletePdfAsync(Guid pdfId) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/pdf/{pdfId}", _token), Ct);

    /// <summary>Gli id dei documenti che il proprietario vede in «I miei documenti».</summary>
    private async Task<List<Guid>> ListedDocumentsAsync()
    {
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/kb-docs?state=all", _token), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
    }

    private async Task<HttpStatusCode> GetAsync(string path)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, path, _token), Ct);
        return response.StatusCode;
    }

    /// <summary>Un collegamento dal gioco al PDF, di chi ha caricato il PDF.</summary>
    private async Task LinkGameToPdfAsync(Guid gameId, Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.EntityLinks.Add(EntityLink.Create(
            MeepleEntityType.Game, gameId,
            MeepleEntityType.KbCard, pdfId,
            EntityLinkType.RelatedTo, EntityLinkScope.User, _userId));
        await db.SaveChangesAsync(Ct);
    }

    private async Task<bool> PdfExistsAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments.IgnoreQueryFilters().AnyAsync(p => p.Id == pdfId, Ct);
    }
}
