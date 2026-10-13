using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="StreamQaSseContractCharacterizationTests"/>: host per classe, database
/// per test (#4050). I doppi stanno ai MARGINI del sistema — embedding, reranker, LLM, cache — e
/// tutto il resto gira davvero: cancello d'accesso, controllo dei documenti pronti, i due bracci
/// del recupero su pgvector e full-text, la fusione, il tier di copyright, la serializzazione SSE.
/// È questo che permette al test di restare lo stesso quando la fetta 5 sposta l'handler sulla
/// pipeline unica.
/// </summary>
public sealed class StreamQaSseContractHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "qa_stream_sse")
{
    internal ScriptedLlmService Llm { get; } = new();
    internal InMemoryAiResponseCache Cache { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmService>();
            services.AddSingleton<ILlmService>(Llm);

            services.RemoveAll<IAiResponseCacheService>();
            services.AddSingleton<IAiResponseCacheService>(Cache);

            services.RemoveAll<ICrossEncoderReranker>();
            services.AddSingleton<ICrossEncoderReranker, PassThroughReranker>();

            // La factory registra un vettore nullo: il coseno con un vettore nullo non è definito,
            // e il braccio vettoriale non troverebbe niente. Qui la domanda punta sull'asse 0.
            services.RemoveAll<IEmbeddingService>();
            services.AddScoped<IEmbeddingService>(_ => QueryVectorEmbeddings.Create());
        }));
}

/// <summary>
/// Fetta 5 di ADR-095 (#4168), caratterizzazione prima dello spostamento: il contratto SSE di
/// <c>POST /api/v1/agents/qa/stream</c> com'è OGGI, scritto prima che l'handler passi sulla
/// pipeline unica e verde prima e dopo il passaggio. Senza, un cambio che l'utente vede — l'ordine
/// degli eventi, la forma di una citazione, il replay dalla cache — passerebbe con tutti i test verdi.
/// </summary>
/// <remarks>
/// <para>
/// Fissa ciò che il client legge (<c>chatClient.qaStream</c> e <c>ChatThreadView.runQaStream</c>):
/// l'involucro <c>{type, data, timestamp}</c> con <c>type</c> numerico, l'ordine dei tipi, le chiavi
/// di ciascun <c>data</c>. Sui valori fissa solo quelli che hanno un significato per il client
/// (messaggi di stato, codici d'errore, testo dei token); i contatori di <c>Complete</c> no, perché
/// <c>completionTokens</c> oggi conta i chunk e non i token, ed è un difetto da correggere, non un
/// contratto. Le chiavi in più restano ammesse: il filo è additivo (D-4 di SP-C).
/// </para>
/// <para>
/// Fuori dall'ambito: le domande di follow-up (evento 8), che non vengono dall'handler ma
/// dall'endpoint, con una seconda chiamata LLM. Le richieste passano <c>generateFollowUps=false</c>.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class StreamQaSseContractCharacterizationTests
    : IClassFixture<StreamQaSseContractHostFixture>, IAsyncLifetime
{
    private const int StateUpdate = (int)StreamingEventType.StateUpdate;
    private const int Citations = (int)StreamingEventType.Citations;
    private const int Complete = (int)StreamingEventType.Complete;
    private const int Error = (int)StreamingEventType.Error;
    private const int Token = (int)StreamingEventType.Token;
    private const int InlineCitation = (int)StreamingEventType.InlineCitation;
    private const int ContinuationAvailable = (int)StreamingEventType.ContinuationAvailable;

    private const string Question = "How many cards does each player draw at setup?";

    // Il chunk che la domanda deve trovare per primo: allineato al vettore della domanda e con le
    // parole della domanda, quindi in testa a entrambi i bracci.
    private const string SetupChunkText =
        "At setup each player draws five cards and keeps them hidden from the other players.";
    private const int SetupChunkPage = 3;
    private const string ScoringChunkText =
        "The longest road card is worth two victory points at the end of the game.";
    private const int ScoringChunkPage = 7;

    private readonly StreamQaSseContractHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private Guid _pdfId;

    public StreamQaSseContractCharacterizationTests(StreamQaSseContractHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync prima di ogni uso della factory, e i doppi con stato si azzerano qui:
        // vivono quanto la classe, non quanto il test.
        await _hostFixture.BeginTestAsync();
        _hostFixture.Llm.Reset();
        _hostFixture.Cache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Coloni di prova");

        // Possesso dichiarato: il cancello RAG (#4155) lascia passare.
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = _userId,
            SharedGameId = _gameId,
            AddedAt = DateTime.UtcNow,
            OwnershipDeclaredAt = DateTime.UtcNow,
        });
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.StreamingResponses",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4168)",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = _userId,
        });
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FreshAnswer_StreamsTheEventsInContractOrder()
    {
        await SeedRulebookAsync(uploadedByAsker: false);
        _hostFixture.Llm.Script = Answer("Yes,", " at the", " start.");

        var events = await StreamQaAsync(Question);

        TypesOf(events).Should().Equal(StateUpdate, Citations, StateUpdate, Token, Token, Token, Complete);
        events.Should().AllSatisfy(e => KeysOf(e).Should().BeEquivalentTo("type", "data", "timestamp"));

        events[0].GetProperty("data").GetProperty("message").GetString().Should().Be("Searching knowledge base...");
        events[2].GetProperty("data").GetProperty("message").GetString().Should().Be("Generating answer...");
        string.Concat(events.Where(e => TypeOf(e) == Token)
                .Select(e => e.GetProperty("data").GetProperty("token").GetString()))
            .Should().Be("Yes, at the start.");

        var complete = events[^1].GetProperty("data");
        KeysOf(complete).Should().Contain(["totalTokens", "confidence", "groundingStatus"]);
        complete.GetProperty("groundingStatus").GetString().Should().Be("Grounded");
        complete.GetProperty("confidence").GetDouble().Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task Citations_HaveTheShapeTheChatReads_AndNoRegionsOnAProtectedRulebook()
    {
        await SeedRulebookAsync(uploadedByAsker: false);
        _hostFixture.Llm.Script = Answer("Five.");

        var events = await StreamQaAsync(Question);

        var citations = CitationsOf(events);
        citations.Should().NotBeEmpty();
        citations.Should().AllSatisfy(c =>
        {
            KeysOf(c).Should().Contain(["text", "source", "page", "line", "score"]);
            KeysOf(c).Should().NotContain(["regions", "charStart", "charEnd"],
                "le regioni sono riservate al tier Full: disegnarle su un manuale protetto è una fuga (DA-4)");
            c.GetProperty("source").GetString().Should().Be($"PDF:{_pdfId}");
            c.GetProperty("line").GetInt32().Should().Be(0);
            c.GetProperty("score").ValueKind.Should().Be(JsonValueKind.Number);
        });

        citations[0].GetProperty("text").GetString().Should().Be(SetupChunkText);
        citations[0].GetProperty("page").GetInt32().Should().Be(SetupChunkPage);
    }

    [Fact]
    public async Task Citations_CarryRegions_WhenTheAskerUploadedTheRulebookAndOwnsTheGame()
    {
        await SeedRulebookAsync(uploadedByAsker: true);
        _hostFixture.Llm.Script = Answer("Five.");

        var events = await StreamQaAsync(Question);

        var first = CitationsOf(events)[0];
        first.GetProperty("charStart").GetInt32().Should().Be(100);
        first.GetProperty("charEnd").GetInt32().Should().Be(100 + SetupChunkText.Length);
        var region = first.GetProperty("regions").EnumerateArray().Should().ContainSingle().Which;
        KeysOf(region).Should().BeEquivalentTo("page", "x", "y", "width", "height");
        region.GetProperty("page").GetInt32().Should().Be(SetupChunkPage);
    }

    [Fact]
    public async Task AnswerQuotingTheRulebook_AddsInlineCitationsAfterComplete()
    {
        await SeedRulebookAsync(uploadedByAsker: false);
        _hostFixture.Llm.Script = Answer(SetupChunkText);

        var events = await StreamQaAsync(Question);

        TypesOf(events).TakeLast(2).Should().Equal(Complete, InlineCitation);
        var match = events[^1].GetProperty("data").GetProperty("citations").EnumerateArray()
            .Should().ContainSingle().Which;
        KeysOf(match).Should().BeEquivalentTo(
            "startOffset", "endOffset", "snippetIndex", "pageNumber", "pdfDocumentId", "confidence");
        match.GetProperty("pdfDocumentId").GetString().Should().Be(_pdfId.ToString());
        match.GetProperty("pageNumber").GetInt32().Should().Be(SetupChunkPage);
    }

    [Fact]
    public async Task TruncatedAnswer_OffersAContinuationAfterComplete()
    {
        await SeedRulebookAsync(uploadedByAsker: false);
        _hostFixture.Llm.Script = AnswerEndingWith("length", "Each player", " draws");

        var events = await StreamQaAsync(Question);

        TypesOf(events).TakeLast(2).Should().Equal(Complete, ContinuationAvailable);
        var data = events[^1].GetProperty("data");
        data.GetProperty("reason").GetString().Should().Be("max_tokens_reached");

        // Il token torna all'endpoint come continuationToken: base64 di un JSON che l'endpoint
        // legge per partialAnswer.
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(
            data.GetProperty("continuationToken").GetString()!));
        using var payload = JsonDocument.Parse(decoded);
        KeysOf(payload.RootElement).Should().BeEquivalentTo("gameId", "originalQuery", "partialAnswer", "threadId");
        payload.RootElement.GetProperty("partialAnswer").GetString().Should().Be("Each player draws");
        payload.RootElement.GetProperty("originalQuery").GetString().Should().Be(Question);
    }

    [Fact]
    public async Task RepeatedQuestion_IsReplayedFromTheCache_WithoutRegionsAndWithoutCallingTheLlm()
    {
        await SeedRulebookAsync(uploadedByAsker: true);
        _hostFixture.Llm.Script = Answer("Five cards", " each.");

        var first = await StreamQaAsync(Question);
        CitationsOf(first)[0].TryGetProperty("regions", out _).Should().BeTrue("la prima risposta è fresca e il tier è Full");

        var replay = await StreamQaAsync(Question);

        _hostFixture.Llm.StreamCalls.Should().Be(1, "la seconda risposta viene dalla cache");
        TypesOf(replay).Should().Equal(StateUpdate, Citations, Token, Token, Token, Complete);
        replay[0].GetProperty("data").GetProperty("message").GetString().Should().Be("Retrieved from cache");
        // La cache è indicizzata per (gioco, domanda), non per utente: le regioni di una risposta
        // Full non si riservono a chi potrebbe non averne diritto.
        CitationsOf(replay).Should().AllSatisfy(c => KeysOf(c).Should().NotContain(["regions", "charStart", "charEnd"]));
        // Il replay simula lo streaming parola per parola.
        string.Concat(replay.Where(e => TypeOf(e) == Token)
                .Select(e => e.GetProperty("data").GetProperty("token").GetString()))
            .Should().Be("Five cards each.");
        replay[^1].GetProperty("data").GetProperty("groundingStatus").GetString().Should().Be("Grounded");
    }

    [Fact]
    public async Task GatewayRefusal_EndsTheStreamWithTheGatewayCode_AndNoComplete()
    {
        await SeedRulebookAsync(uploadedByAsker: false);
        _hostFixture.Llm.Script =
        [
            new StreamChunk(null, ErrorCode: "LLM_BUDGET_EXHAUSTED", ErrorMessage: "Daily AI budget reached."),
        ];

        var events = await StreamQaAsync(Question);

        TypesOf(events).Should().Equal(StateUpdate, Citations, StateUpdate, Error);
        var error = events[^1].GetProperty("data");
        KeysOf(error).Should().BeEquivalentTo("errorMessage", "errorCode");
        error.GetProperty("errorCode").GetString().Should().Be("LLM_BUDGET_EXHAUSTED");
        error.GetProperty("errorMessage").GetString().Should().Be("Daily AI budget reached.");
        _hostFixture.Cache.Count.Should().Be(0, "un rifiuto non è una risposta da riservire");
    }

    [Fact]
    public async Task BlankQuestion_IsRejectedBeforeAnyRetrieval()
    {
        await SeedRulebookAsync(uploadedByAsker: false);

        var events = await StreamQaAsync("   ");

        TypesOf(events).Should().Equal(Error);
        events[0].GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INVALID_QUERY");
        _hostFixture.Llm.StreamCalls.Should().Be(0);
    }

    // ─── Seed ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un manuale indicizzato del gioco: PDF pronto, documento vettoriale, due chunk testuali (il
    /// braccio full-text) e i loro embedding (il braccio vettoriale), collegati da
    /// <c>source_chunk_id</c> come li scrive la pipeline. Il manuale resta nella categoria di default
    /// (regolamento, protetto): diventa Full solo se chi chiede l'ha caricato e possiede il gioco.
    /// </summary>
    private async Task SeedRulebookAsync(bool uploadedByAsker)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var uploaderId = _userId;
        if (!uploadedByAsker)
        {
            (uploaderId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        _pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfId,
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = uploaderId,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = _gameId,
        });

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = _gameId,
            PdfDocumentId = _pdfId,
            ChunkCount = 2,
            TotalCharacters = SetupChunkText.Length + ScoringChunkText.Length,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = QueryVectorEmbeddings.Dimensions,
        });

        var setupChunk = Chunk(0, SetupChunkPage, SetupChunkText, charStart: 100,
            boundingBoxesJson: $$"""[{"page":{{SetupChunkPage}},"x":0.1,"y":0.2,"width":0.5,"height":0.05}]""");
        var scoringChunk = Chunk(1, ScoringChunkPage, ScoringChunkText, charStart: 900, boundingBoxesJson: null);
        db.TextChunks.AddRange(setupChunk, scoringChunk);
        await db.SaveChangesAsync(Ct);

        // Il chunk del setup coincide con la domanda (coseno 1); quello del punteggio le resta sopra
        // la soglia (coseno 0,8) ma dietro.
        await InsertEmbeddingAsync(db, vectorDocumentId, setupChunk, QueryVectorEmbeddings.Axis(1f, 0f));
        await InsertEmbeddingAsync(db, vectorDocumentId, scoringChunk, QueryVectorEmbeddings.Axis(0.8f, 0.6f));
    }

    private TextChunkEntity Chunk(int index, int page, string text, int charStart, string? boundingBoxesJson) => new()
    {
        Id = Guid.NewGuid(),
        GameId = _gameId,
        PdfDocumentId = _pdfId,
        Content = text,
        ChunkIndex = index,
        PageNumber = page,
        CharacterCount = text.Length,
        CharStart = charStart,
        CharEnd = charStart + text.Length,
        BoundingBoxesJson = boundingBoxesJson,
    };

    private async Task InsertEmbeddingAsync(
        MeepleAiDbContext db, Guid vectorDocumentId, TextChunkEntity chunk, float[] vector)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pgvector_embeddings
                (id, vector_document_id, game_id, text_content, vector, model,
                 chunk_index, page_number, created_at, lang, source_chunk_id, is_translation, role_tags)
            VALUES
                (@id, @vdId, @gameId, @text, @vec, 'test-model',
                 @chunkIndex, @pageNumber, NOW(), 'en', @sourceChunkId, false, 0)
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@vdId", vectorDocumentId);
        command.Parameters.AddWithValue("@gameId", _gameId);
        command.Parameters.AddWithValue("@text", chunk.Content);
        command.Parameters.AddWithValue("@vec", new Pgvector.Vector(vector));
        command.Parameters.AddWithValue("@chunkIndex", chunk.ChunkIndex);
        command.Parameters.AddWithValue("@pageNumber", chunk.PageNumber!.Value);
        command.Parameters.AddWithValue("@sourceChunkId", chunk.Id);
        await command.ExecuteNonQueryAsync(Ct);
    }

    // ─── Richiesta e lettura del filo ───────────────────────────────────────────

    private async Task<List<JsonElement>> StreamQaAsync(string query)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", _token,
                new { gameId = _gameId.ToString(), query }),
            Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(trimmed["data:".Length..].Trim());
            events.Add(doc.RootElement.Clone());
        }

        return events;
    }

    private static List<StreamChunk> Answer(params string[] tokens) => AnswerEndingWith("stop", tokens);

    private static List<StreamChunk> AnswerEndingWith(string finishReason, params string[] tokens)
    {
        var chunks = tokens.Select(t => new StreamChunk(t)).ToList();
        chunks.Add(new StreamChunk(
            null,
            Usage: new LlmUsage(120, tokens.Length, 120 + tokens.Length),
            Cost: new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = "test-model", Provider = "Test" },
            IsFinal: true,
            FinishReason: finishReason));
        return chunks;
    }

    private static int TypeOf(JsonElement evt) => evt.GetProperty("type").GetInt32();

    private static List<int> TypesOf(IEnumerable<JsonElement> events) => events.Select(TypeOf).ToList();

    private static List<string> KeysOf(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).ToList();

    private static List<JsonElement> CitationsOf(IEnumerable<JsonElement> events) =>
        events.Single(e => TypeOf(e) == Citations)
            .GetProperty("data").GetProperty("citations").EnumerateArray().ToList();
}

/// <summary>Un LLM a copione: lo stream restituisce i chunk di <see cref="Script"/>, e conta le chiamate.</summary>
internal sealed class ScriptedLlmService : ILlmService
{
    private int _streamCalls;

    public IReadOnlyList<StreamChunk> Script { get; set; } = [];

    public int StreamCalls => Volatile.Read(ref _streamCalls);

    public void Reset()
    {
        Script = [];
        Interlocked.Exchange(ref _streamCalls, 0);
    }

    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Interlocked.Increment(ref _streamCalls);
        foreach (var chunk in Script)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return chunk;
        }
    }

    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure("Not scripted"));

    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure("Not scripted"));

    public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel, string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure("Not scripted"));
}

/// <summary>
/// Una cache in memoria che passa per JSON come la cache vera passa per Redis: un campo che non
/// sopravvive alla serializzazione non sopravvive nemmeno qui.
/// </summary>
internal sealed class InMemoryAiResponseCache : IAiResponseCacheService
{
    private readonly ConcurrentDictionary<string, string> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;

    public void Clear() => _entries.Clear();

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class =>
        Task.FromResult(_entries.TryGetValue(cacheKey, out var json) ? JsonSerializer.Deserialize<T>(json) : null);

    public Task SetAsync<T>(string cacheKey, T response, int ttlSeconds = 86400, CancellationToken ct = default)
        where T : class
    {
        _entries[cacheKey] = JsonSerializer.Serialize(response);
        return Task.CompletedTask;
    }

    public string GenerateQaCacheKey(string gameId, string query) => $"qa:{gameId}:{query}";

    public string GenerateExplainCacheKey(string gameId, string topic) => $"explain:{gameId}:{topic}";

    public string GenerateSetupCacheKey(string gameId) => $"setup:{gameId}";

    public Task InvalidateGameAsync(string gameId, CancellationToken ct = default)
    {
        foreach (var key in _entries.Keys.Where(k => k.Contains(gameId, StringComparison.Ordinal)))
            _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task InvalidateEndpointAsync(string gameId, AiCacheEndpoint endpoint, CancellationToken ct = default) =>
        InvalidateGameAsync(gameId, ct);

    public Task InvalidateByCacheTagAsync(string tag, CancellationToken ct = default) => Task.CompletedTask;

    public Task<CacheStats> GetCacheStatsAsync(string? gameId = null, CancellationToken ct = default) =>
        Task.FromResult(new CacheStats());

    public Task RecordCacheAccessAsync(string gameId, string questionHash, bool isHit, CancellationToken ct = default) =>
        Task.CompletedTask;
}

/// <summary>Un reranker che non riordina: lascia l'ordine della fusione e taglia ai primi K.</summary>
internal sealed class PassThroughReranker : ICrossEncoderReranker
{
    public Task<RerankResult> RerankAsync(
        string query, IReadOnlyList<RerankChunk> chunks, int? topK = null, CancellationToken cancellationToken = default)
    {
        var kept = chunks
            .Take(topK ?? chunks.Count)
            .Select(c => new RerankedChunk(c.Id, c.Content, c.OriginalScore, c.OriginalScore, c.Metadata))
            .ToList();
        return Task.FromResult(new RerankResult(kept, "pass-through", 0));
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>Embedding della domanda: un vettore unitario sull'asse 0, a 768 dimensioni come la colonna.</summary>
internal static class QueryVectorEmbeddings
{
    public const int Dimensions = 768;

    public static float[] Axis(float first, float second)
    {
        var vector = new float[Dimensions];
        vector[0] = first;
        vector[1] = second;
        return vector;
    }

    public static IEmbeddingService Create()
    {
        var success = EmbeddingResult.CreateSuccess(new List<float[]> { Axis(1f, 0f) });
        var mock = new Mock<IEmbeddingService>();
        mock.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);
        mock.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);
        mock.Setup(s => s.GenerateEmbeddingAsync(
                It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);
        mock.Setup(s => s.GenerateEmbeddingAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success);
        return mock.Object;
    }
}
