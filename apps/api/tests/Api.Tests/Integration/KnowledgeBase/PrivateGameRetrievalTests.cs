using System.Net;
using System.Text.Json;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #4244: una domanda su un gioco PRIVATO trova i passaggi del suo PDF, con entrambi i
/// bracci del recupero, e cita il PDF.
/// </summary>
/// <remarks>
/// <para>
/// Il gioco privato sta in una colonna sua (<c>PrivateGameId</c>) su <c>text_chunks</c> e
/// <c>vector_documents</c>, perché <c>GameId</c> è una chiave esterna verso <c>shared_games</c>.
/// Chi legge «i documenti del gioco» deve guardare entrambe le colonne: il braccio lessicale, la
/// risoluzione del PDF per le citazioni, il controllo dei documenti ancora in elaborazione.
/// </para>
/// <para>
/// Su Postgres, via HTTP: il provider in memoria non applica chiavi esterne, ed è per questo che i
/// test precedenti su questo percorso passavano.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed class PrivateGameRetrievalTests
    : IClassFixture<AgentProfileFieldEffectHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";

    private const string BothArms = "At setup each player draws five cards and keeps them hidden.";
    private const string LexicalOnly = "Each player draws two extra cards at setup when the long variant is used.";
    private const string VectorOnly = "Deal a starting hand of five to everybody.";

    private readonly AgentProfileFieldEffectHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _privateGameId;
    private Guid _pdfId;

    public PrivateGameRetrievalTests(AgentProfileFieldEffectHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _hostFixture.Llm.Reset();
        _hostFixture.Reranker.Reset();
        _hostFixture.Cache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);

        _privateGameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = _privateGameId,
            OwnerId = _userId,
            Title = "Il mio prototipo",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.StreamingResponses",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4244)",
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

        _pdfId = await IndexPrivatePdfAsync(db, processingState: "Ready",
            (BothArms, QueryVectorEmbeddings.Axis(1f, 0f)),
            (LexicalOnly, QueryVectorEmbeddings.Axis(0f, 1f)),
            (VectorOnly, QueryVectorEmbeddings.Axis(1f, 0f)));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheOwner_GetsAnAnswerFromBothArms()
    {
        var events = await AskAsync(_token);

        ErrorsOf(events).Should().BeEmpty();
        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("il modello è stato interrogato")
            .And.Contain(BothArms)
            .And.Contain(LexicalOnly, "il braccio lessicale legge i passaggi del gioco privato")
            .And.Contain(VectorOnly, "il braccio vettoriale legge gli embedding del gioco privato");
    }

    /// <summary>
    /// La citazione porta l'id del PDF, che è ciò che il visualizzatore apre. Senza il documento
    /// vettoriale del gioco privato, la citazione ricadeva sull'id del vettore e il PDF non si apriva.
    /// </summary>
    [Fact]
    public async Task TheCitations_PointToThePdf()
    {
        var events = await AskAsync(_token);

        var citations = events
            .Where(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations)
            .SelectMany(e => e.GetProperty("data").GetProperty("citations").EnumerateArray())
            .ToList();
        citations.Should().NotBeEmpty();
        citations.Should().OnlyContain(c => c.GetProperty("source").GetString() == $"PDF:{_pdfId}");
    }

    /// <summary>
    /// Un PDF del gioco privato ancora in elaborazione ferma la domanda, come per un gioco del
    /// catalogo. Prima il controllo non vedeva i PDF privati e passava sempre.
    /// </summary>
    [Fact]
    public async Task APdfStillBeingProcessed_StopsTheQuestion()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Extracting");
        }

        var events = await AskAsync(_token);

        ErrorsOf(events).Should().Equal("DOCUMENT_PROCESSING");
    }

    /// <summary>Il controllo d'accesso: il gioco privato di un altro non risponde.</summary>
    [Fact]
    public async Task AnotherUser_GetsNothingFromThePrivateGame()
    {
        string strangerToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var events = await AskAsync(strangerToken);

        ErrorsOf(events).Should().NotBeEmpty();
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("a un estraneo non si costruisce nessun prompt");
    }

    /// <summary>
    /// La domanda singola (<c>/knowledge-base/ask</c>) ha la sua copia del controllo «documenti in
    /// elaborazione». Guardava solo il catalogo: per un gioco privato non trovava PDF e rispondeva
    /// da un contenuto parziale.
    /// </summary>
    [Fact]
    public async Task APdfStillBeingProcessed_StopsTheSingleQuestionToo()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await IndexPrivatePdfAsync(db, processingState: "Extracting");
        }

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/knowledge-base/ask", _token,
            new { gameId = _privateGameId.ToString(), query = Question }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        // La rotta non espone il codice d'errore: il rifiuto si riconosce dal testo e dal fatto
        // che il modello non viene chiamato.
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("answer").GetString().Should().Contain("still being processed", body);
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("la domanda si ferma prima del modello");
    }

    /// <summary>
    /// <c>GET /games/{id}/knowledge-base</c> non controlla chi chiede: per un gioco del catalogo
    /// dice a chiunque se c'è una KB. Di un gioco privato non deve dire nulla a un estraneo — né
    /// che è indicizzato, né quanti documenti e passaggi ha.
    /// </summary>
    [Fact]
    public async Task AnotherUser_LearnsNothingAboutTheKbOfThePrivateGame()
    {
        string strangerToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/games/{_privateGameId}/knowledge-base", strangerToken), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("isIndexed").GetBoolean().Should().BeFalse(body);
        json.RootElement.GetProperty("documentCount").GetInt32().Should().Be(0, body);
    }

    /// <summary>
    /// Il braccio lessicale toglie dalla domanda le parole del titolo del gioco (#3768): dentro il
    /// corpus di quel gioco non distinguono nulla, e portano in alto copertina e colophon. Il titolo
    /// veniva cercato solo nel catalogo, quindi per un gioco privato restavano nella ricerca.
    /// </summary>
    [Fact]
    public async Task TheLexicalArm_KnowsTheTitleOfThePrivateGame()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var keywordSearch = scope.ServiceProvider.GetRequiredService<Api.Services.IKeywordSearchService>();

        var title = await keywordSearch.ResolveGameTitleAsync(_privateGameId, Ct);

        title.Should().Be("Il mio prototipo");
    }

    /// <summary>Un gioco privato cancellato non ha più un titolo da leggere.</summary>
    [Fact]
    public async Task TheLexicalArm_DoesNotReadTheTitleOfADeletedPrivateGame()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var deleted = await db.PrivateGames.AsTracking().SingleAsync(g => g.Id == _privateGameId, Ct);
        deleted.IsDeleted = true;
        deleted.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(Ct);

        var keywordSearch = scope.ServiceProvider.GetRequiredService<Api.Services.IKeywordSearchService>();
        var title = await keywordSearch.ResolveGameTitleAsync(_privateGameId, Ct);

        title.Should().BeNull();
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private async Task<List<JsonElement>> AskAsync(string token)
    {
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", token,
            new { gameId = _privateGameId.ToString(), query = Question }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

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

    private static List<string?> ErrorsOf(IEnumerable<JsonElement> events) =>
        events
            .Where(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error)
            .Select(e => e.GetProperty("data").GetProperty("errorCode").GetString())
            .ToList();

    /// <summary>
    /// Il PDF di un gioco privato come lo lascia l'indicizzazione: il gioco nella colonna
    /// <c>PrivateGameId</c> di <c>text_chunks</c> e <c>vector_documents</c> (con <c>GameId</c> null),
    /// e nel <c>game_id</c> unico degli embedding.
    /// </summary>
    private async Task<Guid> IndexPrivatePdfAsync(
        MeepleAiDbContext db, string processingState, params (string Text, float[] Vector)[] chunks)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = $"private-{pdfId:N}.pdf",
            FilePath = $"/tmp/private-{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = _userId,
            ProcessingState = processingState,
            ProcessedAt = string.Equals(processingState, "Ready", StringComparison.Ordinal) ? DateTime.UtcNow : null,
            PrivateGameId = _privateGameId,
        });
        await db.SaveChangesAsync(Ct);

        if (chunks.Length == 0)
        {
            return pdfId;
        }

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = null,
            PrivateGameId = _privateGameId,
            PdfDocumentId = pdfId,
            ChunkCount = chunks.Length,
            TotalCharacters = chunks.Sum(c => c.Text.Length),
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = QueryVectorEmbeddings.Dimensions,
        });

        var entities = chunks.Select((c, i) => new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = null,
            PrivateGameId = _privateGameId,
            PdfDocumentId = pdfId,
            Content = c.Text,
            ChunkIndex = i,
            PageNumber = i + 1,
            CharacterCount = c.Text.Length,
        }).ToList();
        db.TextChunks.AddRange(entities);
        await db.SaveChangesAsync(Ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

        for (var i = 0; i < entities.Count; i++)
        {
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
            command.Parameters.AddWithValue("@gameId", _privateGameId);
            command.Parameters.AddWithValue("@text", entities[i].Content);
            command.Parameters.AddWithValue("@vec", new Pgvector.Vector(chunks[i].Vector));
            command.Parameters.AddWithValue("@chunkIndex", i);
            command.Parameters.AddWithValue("@pageNumber", i + 1);
            command.Parameters.AddWithValue("@sourceChunkId", entities[i].Id);
            await command.ExecuteNonQueryAsync(Ct);
        }

        return pdfId;
    }
}
