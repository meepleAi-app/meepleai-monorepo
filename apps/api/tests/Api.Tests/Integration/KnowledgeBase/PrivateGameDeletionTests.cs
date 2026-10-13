using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="PrivateGameDeletionTests"/>. Gli host di integrazione montano una
/// cache ibrida che richiama sempre la fabbrica; qui ce n'è una che ricorda davvero, perché
/// l'elenco «I miei documenti» in produzione sta in cache, e un test che non ha cache non può
/// dire se la cancellazione la invalida.
/// </summary>
public sealed class PrivateGameDeletionHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "pg_deletion")
{
    internal RememberingHybridCache HybridCache { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHybridCacheService>();
            services.AddSingleton<IHybridCacheService>(HybridCache);
        }));
}

/// <summary>Una cache ibrida in memoria che ricorda i valori e li toglie per etichetta.</summary>
internal sealed class RememberingHybridCache : IHybridCacheService
{
    private readonly ConcurrentDictionary<string, (object Value, string[] Tags)> _entries =
        new(StringComparer.Ordinal);

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
    {
        if (_entries.TryGetValue(cacheKey, out var hit))
        {
            return (T)hit.Value;
        }

        var value = await factory(ct);
        _entries[cacheKey] = (value, tags ?? []);
        return value;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        _entries.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        var removed = 0;
        foreach (var (key, entry) in _entries)
        {
            if (entry.Tags.Contains(tag, StringComparer.Ordinal) && _entries.TryRemove(key, out _))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }

    public async Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default)
    {
        var removed = 0;
        foreach (var tag in tags)
        {
            removed += await RemoveByTagAsync(tag, ct);
        }

        return removed;
    }

    public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default) =>
        RemoveByTagAsync(tag, ct);

    public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) =>
        Task.FromResult(new HybridCacheStats());

    public void Clear() => _entries.Clear();
}

/// <summary>
/// Issue #4244 (consegna C): cancellato un gioco privato, del suo contenuto non resta niente —
/// le chat, i passaggi, il documento vettoriale, gli embedding, il PDF.
/// </summary>
/// <remarks>
/// <para>
/// La cancellazione del gioco è logica (<c>is_deleted</c>), quindi le chiavi esterne con
/// <c>ON DELETE CASCADE</c> verso <c>private_games</c> non scattano mai: senza una pulizia
/// esplicita il contenuto resterebbe nel database, irraggiungibile e per sempre.
/// </para>
/// <para>
/// Su Postgres, via HTTP. Ogni test ha un secondo gioco privato dello stesso proprietario che
/// fa da controllo: la pulizia non deve portarsi via nient'altro.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed class PrivateGameDeletionTests
    : IClassFixture<PrivateGameDeletionHostFixture>, IAsyncLifetime
{
    /// <summary>Quante righe di un gioco privato ci sono, tabella per tabella.</summary>
    private sealed record Content(int Threads, int Pdfs, int VectorDocuments, int Passages, int Embeddings);

    private static readonly Content Nothing = new(0, 0, 0, 0, 0);

    private readonly PrivateGameDeletionHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private Guid _pdfId;
    private Guid _otherGameId;
    private Guid _otherPdfId;

    public PrivateGameDeletionTests(PrivateGameDeletionHostFixture hostFixture)
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

        (_gameId, _pdfId) = await SeedPrivateGameAsync(db, "Il mio prototipo",
            ("At setup each player draws five cards.", QueryVectorEmbeddings.Axis(1f, 0f)),
            ("The game ends when the deck runs out.", QueryVectorEmbeddings.Axis(0f, 1f)));
        (_otherGameId, _otherPdfId) = await SeedPrivateGameAsync(db, "Un altro prototipo",
            ("In the other prototype each player draws seven tiles.", QueryVectorEmbeddings.Axis(1f, 0f)));

        await OpenThreadAsync(_gameId);
        await OpenThreadAsync(_otherGameId);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DeletingAPrivateGame_LeavesNoneOfItsContent()
    {
        // Il punto di partenza: se il gioco non avesse niente, «non resta niente» non proverebbe nulla.
        (await ContentOfAsync(_gameId)).Should().Be(
            new Content(Threads: 1, Pdfs: 1, VectorDocuments: 1, Passages: 2, Embeddings: 2));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
    }

    /// <summary>
    /// Non tutto pende da un PDF del gioco. Gli embedding non hanno una chiave esterna verso il
    /// documento vettoriale: una cancellazione finita a metà li lascia soli, legati al gioco dal
    /// solo <c>game_id</c>. E i passaggi nominano il gioco per conto loro, anche se il PDF non lo fa.
    /// </summary>
    [Fact]
    public async Task ContentThatHangsFromNoPdfOfTheGame_IsRemovedToo()
    {
        var detachedPdfId = await SeedContentOutsideThePdfsOfTheGameAsync(_gameId);
        (await ContentOfAsync(_gameId)).Should().Be(
            new Content(Threads: 1, Pdfs: 1, VectorDocuments: 2, Passages: 3, Embeddings: 4));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        // Il PDF che non nomina il gioco è ancora lì: quelle righe non sono andate via con lui.
        (await PdfExistsAsync(detachedPdfId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingAPrivateGame_LeavesTheOtherGamesOfTheOwnerAlone()
    {
        var before = await ContentOfAsync(_otherGameId);
        before.Should().Be(new Content(Threads: 1, Pdfs: 1, VectorDocuments: 1, Passages: 1, Embeddings: 1));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_otherGameId)).Should().Be(before);
    }

    [Fact]
    public async Task AnotherUser_CannotDeleteThePrivateGame_AndNothingIsRemoved()
    {
        var before = await ContentOfAsync(_gameId);
        string strangerToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var response = await DeleteGameAsync(_gameId, strangerToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_gameId)).Should().Be(before);
        (await IsDeletedAsync(_gameId)).Should().BeFalse();
    }

    /// <summary>
    /// «I miei documenti» (<c>GET /api/v1/kb-docs</c>) tiene l'elenco in cache per cinque minuti.
    /// Chi l'ha guardato prima di cancellare il gioco non deve ritrovarci un documento che non
    /// esiste più. Regge solo perché la fixture monta una cache che ricorda: con quella degli altri
    /// host di integrazione, che richiama sempre la fabbrica, passerebbe per niente.
    /// </summary>
    [Fact]
    public async Task TheOwnersDocumentList_NoLongerShowsTheDocumentsOfTheDeletedGame()
    {
        // La prima lettura mette l'elenco in cache: è il caso che conta.
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _pdfId, _otherPdfId });

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _otherPdfId });
    }

    /// <summary>
    /// La cancellazione del gioco resta logica: sparisce il contenuto, non la riga del gioco.
    /// Una cancellazione fisica violerebbe <c>CK_UserLibraryEntry_GameSource</c>.
    /// </summary>
    [Fact]
    public async Task TheGameItself_IsStillSoftDeleted()
    {
        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await IsDeletedAsync(_gameId)).Should().BeTrue();
        (await IsDeletedAsync(_otherGameId)).Should().BeFalse();
    }

    /// <summary>
    /// Ripetere la cancellazione non deve fare danni: il gioco non c'è più per il suo proprietario,
    /// e il contenuto degli altri giochi resta dov'è.
    /// </summary>
    [Fact]
    public async Task DeletingTwice_AnswersNotFound_AndRemovesNothingElse()
    {
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var before = await ContentOfAsync(_otherGameId);

        var again = await DeleteGameAsync(_gameId, _token);

        again.StatusCode.Should().Be(HttpStatusCode.NotFound, await again.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_otherGameId)).Should().Be(before);
    }

    // ─── Supporto ────────────────────────────────────────────────

    private Task<HttpResponseMessage> DeleteGameAsync(Guid gameId, string token) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/private-games/{gameId}", token), Ct);

    private async Task<(Guid GameId, Guid PdfId)> SeedPrivateGameAsync(
        MeepleAiDbContext db, string title, params (string Text, float[] Vector)[] passages)
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
        var pdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, gameId, "Ready", Ct, passages);
        return (gameId, pdfId);
    }

    /// <summary>Un thread del proprietario sul gioco, con un messaggio dentro.</summary>
    private async Task OpenThreadAsync(Guid gameId)
    {
        var created = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/chat-threads", _token,
            new { gameId, title = "Chat sul prototipo" }), Ct);
        var body = await created.Content.ReadAsStringAsync(Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created, body);
        using var thread = JsonDocument.Parse(body);
        var threadId = thread.RootElement.GetProperty("id").GetGuid();

        var added = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/messages", _token,
            new { content = "Quante carte si pescano?", role = "user" }), Ct);
        added.IsSuccessStatusCode.Should().BeTrue(await added.Content.ReadAsStringAsync(Ct));
    }

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

    private async Task<Content> ContentOfAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return new Content(
            Threads: await db.ChatThreads.IgnoreQueryFilters().CountAsync(t => t.PrivateGameId == gameId, Ct),
            Pdfs: await db.PdfDocuments.IgnoreQueryFilters().CountAsync(p => p.PrivateGameId == gameId, Ct),
            VectorDocuments: await db.VectorDocuments.IgnoreQueryFilters().CountAsync(v => v.PrivateGameId == gameId, Ct),
            Passages: await db.TextChunks.IgnoreQueryFilters().CountAsync(c => c.PrivateGameId == gameId, Ct),
            Embeddings: await db.PgVectorEmbeddings.IgnoreQueryFilters().CountAsync(e => e.GameId == gameId, Ct));
    }

    /// <summary>
    /// Contenuto del gioco che la cancellazione dei suoi PDF non raggiunge: un PDF che non nomina
    /// il gioco, con passaggi, documento vettoriale ed embedding che invece lo nominano; e un
    /// embedding rimasto senza documento vettoriale.
    /// </summary>
    private async Task<Guid> SeedContentOutsideThePdfsOfTheGameAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var detachedPdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, gameId, "Ready", Ct,
            ("A passage whose PDF does not name the game.", QueryVectorEmbeddings.Axis(0f, 1f)));
        await db.PdfDocuments
            .Where(p => p.Id == detachedPdfId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PrivateGameId, (Guid?)null), Ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);
        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pgvector_embeddings
                (id, vector_document_id, game_id, text_content, vector, model,
                 chunk_index, page_number, created_at, lang, is_translation, role_tags)
            VALUES
                (@id, @vdId, @gameId, 'An embedding left without its vector document.', @vec, 'test-model',
                 0, 1, NOW(), 'en', false, 0)
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@vdId", Guid.NewGuid());
        command.Parameters.AddWithValue("@gameId", gameId);
        command.Parameters.AddWithValue("@vec", new Pgvector.Vector(QueryVectorEmbeddings.Axis(1f, 0f)));
        await command.ExecuteNonQueryAsync(Ct);

        return detachedPdfId;
    }

    private async Task<bool> PdfExistsAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments.IgnoreQueryFilters().AnyAsync(p => p.Id == pdfId, Ct);
    }

    private async Task<bool> IsDeletedAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PrivateGames.IgnoreQueryFilters()
            .Where(g => g.Id == gameId)
            .Select(g => g.IsDeleted)
            .SingleAsync(Ct);
    }
}
