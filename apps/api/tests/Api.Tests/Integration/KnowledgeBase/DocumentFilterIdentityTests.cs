using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #4243: il filtro per documento ha una sola identità, l'id del PDF, nei due bracci del
/// recupero. Prima il braccio vettoriale confrontava l'elenco con <c>vector_documents.Id</c> e
/// quello lessicale con <c>pdf_documents.Id</c>: qualunque id passasse il chiamante, uno dei due
/// bracci restava vuoto.
/// </summary>
/// <remarks>
/// <para>
/// Su Postgres vero, perché i due id divergono solo lì: un doppio che usa lo stesso id per il
/// documento vettoriale e per il PDF non vede il difetto. I test della ricerca globale con i facet
/// lo dimostrano: sostituiscono la ricerca con un doppio e asseriscono solo che l'elenco passi.
/// </para>
/// <para>
/// Il corpus è fatto perché ogni braccio si riconosca dal testo che restituisce. Il PDF A ha un
/// passaggio che trovano entrambi i bracci, uno che trova solo il lessicale (le parole della
/// domanda, vettore ortogonale) e uno che trova solo il vettoriale (vettore della domanda, nessuna
/// parola in comune). Il PDF B ha un passaggio che trovano entrambi: senza filtro esce, col filtro no.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4243")]
public sealed class DocumentFilterIdentityTests
    : IClassFixture<AgentProfileFieldEffectHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";

    private const string BothArmsInA = "At setup each player draws five cards and keeps them hidden.";
    private const string LexicalOnlyInA = "Each player draws two extra cards at setup when the long variant is used.";
    private const string VectorOnlyInA = "Deal a starting hand of five to everybody.";
    private const string BothArmsInB = "In the expansion each player draws seven cards at setup.";
    private const string VectorOnlyInB = "Hand seven of them to everybody in the expansion.";

    private readonly AgentProfileFieldEffectHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private IndexedPdf _pdfA = null!;
    private IndexedPdf _pdfB = null!;

    public DocumentFilterIdentityTests(AgentProfileFieldEffectHostFixture hostFixture)
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
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Coloni dei due manuali");
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
            Description = "Integration test seed (#4243)",
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

        _pdfA = await IndexPdfAsync(db, "rulebook-a.pdf", _gameId, documentType: "base",
            (BothArmsInA, QueryVectorEmbeddings.Axis(1f, 0f)),
            (LexicalOnlyInA, QueryVectorEmbeddings.Axis(0f, 1f)),
            (VectorOnlyInA, QueryVectorEmbeddings.Axis(1f, 0f)));
        _pdfB = await IndexPdfAsync(db, "expansion-b.pdf", _gameId, documentType: "expansion",
            (BothArmsInB, QueryVectorEmbeddings.Axis(1f, 0f)),
            (VectorOnlyInB, QueryVectorEmbeddings.Axis(1f, 0f)));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Il controllo ───────────────────────────────────────────────────────────

    /// <summary>
    /// Senza filtro il corpus restituisce entrambi i PDF. Senza questo, i test col filtro
    /// passerebbero anche se il PDF B non fosse trovabile per conto suo.
    /// </summary>
    [Fact]
    public async Task WithoutAFilter_BothPdfsAreFound()
    {
        var texts = await SearchAsync("hybrid", documentIds: null);

        texts.Should().Contain(BothArmsInA).And.Contain(BothArmsInB);
    }

    // ─── La ricerca, come la chiamano KbQuality e la ricerca globale: con l'id del PDF ───

    [Fact]
    public async Task VectorArm_WithThePdfId_ReturnsThatPdfOnly()
    {
        var texts = await SearchAsync("vector", documentIds: [_pdfA.PdfId]);

        texts.Should().BeEquivalentTo([BothArmsInA, VectorOnlyInA]);
    }

    [Fact]
    public async Task Hybrid_WithThePdfId_UsesBothArms_OnThatPdfOnly()
    {
        var texts = await SearchAsync("hybrid", documentIds: [_pdfA.PdfId]);

        texts.Should().BeEquivalentTo([BothArmsInA, LexicalOnlyInA, VectorOnlyInA]);
    }

    /// <summary>
    /// L'altra direzione: il filtro sceglie il PDF nominato, non sempre lo stesso. Anche qui un
    /// passaggio lo trova solo il braccio vettoriale, altrimenti il test passerebbe col solo lessicale.
    /// </summary>
    [Fact]
    public async Task Hybrid_WithTheOtherPdfId_UsesBothArms_OnTheOtherPdfOnly()
    {
        var texts = await SearchAsync("hybrid", documentIds: [_pdfB.PdfId]);

        texts.Should().BeEquivalentTo([BothArmsInB, VectorOnlyInB]);
    }

    /// <summary>
    /// Una lista vuota vuol dire «nessun filtro», per entrambi i bracci. Il vettoriale la leggeva
    /// già così; il lessicale la prendeva come filtro e non teneva niente.
    /// </summary>
    [Fact]
    public async Task AnEmptyFilter_IsNoFilter_ForBothArms()
    {
        var withoutFilter = await SearchAsync("hybrid", documentIds: null);
        var withAnEmptyFilter = await SearchAsync("hybrid", documentIds: []);

        withoutFilter.Should().Contain(LexicalOnlyInA, "il controllo: senza filtro il braccio lessicale c'è");
        withAnEmptyFilter.Should().Equal(withoutFilter);
    }

    /// <summary>
    /// KbQuality dal suo adattatore vero: la valutazione deve misurare lo stesso recupero che
    /// produce le risposte, cioè entrambi i bracci. Tre passaggi nel PDF A, tre risultati.
    /// </summary>
    [Fact]
    public async Task KbQuality_SearchesTheDocumentWithBothArms()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IKbSearchProvider>();

        var result = await provider.SearchAsync(_pdfA.PdfId, Question, topK: 10, Ct);

        result.RetrievedChunkIds.Should().HaveCount(3);
    }

    // ─── La ricerca globale: i facet costruiscono un elenco di id di PDF ───

    /// <summary>
    /// Un facet che non esclude nessun documento non deve cambiare i risultati. Prima li cambiava:
    /// l'elenco di id di PDF che il facet costruisce svuotava il braccio vettoriale.
    /// </summary>
    [Fact]
    public async Task GlobalSearch_AFacetThatMatchesEveryDocument_ChangesNothing()
    {
        var withoutFacet = await GlobalSearchAsync(language: null);
        var withFacet = await GlobalSearchAsync(language: "en");

        withoutFacet.Should().NotBeEmpty("il controllo: senza facet la ricerca trova qualcosa");
        withFacet.Should().Equal(withoutFacet);
    }

    /// <summary>
    /// Un facet che esclude un documento lo toglie da entrambi i bracci, e sui documenti rimasti i
    /// due bracci lavorano ancora: il passaggio che trova solo il vettoriale è fra i risultati.
    /// </summary>
    [Fact]
    public async Task GlobalSearch_AFacetThatExcludesADocument_KeepsBothArmsOnTheRest()
    {
        var results = await GlobalSearchAsync(language: null, docType: ["base"]);

        results.Should().Contain(r => r.EndsWith(BothArmsInA, StringComparison.Ordinal))
            .And.Contain(r => r.EndsWith(VectorOnlyInA, StringComparison.Ordinal))
            .And.NotContain(r => r.EndsWith(BothArmsInB, StringComparison.Ordinal))
            .And.NotContain(r => r.EndsWith(VectorOnlyInB, StringComparison.Ordinal));
    }

    // ─── La chat: il client manda l'id del documento vettoriale (GameDocumentDto.Id) ───

    /// <summary>
    /// Il passaggio che trova solo il braccio lessicale arriva nel prompt: prima il filtro lo
    /// toglieva, perché confrontava l'id del documento vettoriale con quello del PDF.
    /// </summary>
    [Fact]
    public async Task Chat_WithTheVectorDocumentId_UsesBothArms_OnThatPdfOnly()
    {
        await AskAsync(documentIds: [_pdfA.VectorDocumentId]);

        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("il modello è stato interrogato")
            .And.Contain(BothArmsInA)
            .And.Contain(LexicalOnlyInA)
            .And.Contain(VectorOnlyInA)
            .And.NotContain(BothArmsInB);
    }

    [Fact]
    public async Task Chat_WithoutAFilter_UsesBothPdfs()
    {
        await AskAsync(documentIds: null);

        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("il modello è stato interrogato")
            .And.Contain(BothArmsInA)
            .And.Contain(BothArmsInB);
    }

    /// <summary>
    /// Un filtro che non nomina nessun documento di questo gioco non trova niente. Non deve
    /// diventare «nessun filtro»: più in basso una lista vuota vuol dire proprio quello, e la
    /// domanda riceverebbe una risposta da tutti i manuali.
    /// </summary>
    [Fact]
    public async Task Chat_WithAnIdThatNamesNoDocument_FindsNothing()
    {
        var errors = await AskForErrorsAsync(documentIds: [Guid.NewGuid()]);

        errors.Should().Equal("NO_RESULTS");
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("senza passaggi il modello non si interroga");
    }

    /// <summary>
    /// Il documento di un altro gioco non entra dal filtro: si risolvono solo i documenti del gioco
    /// della domanda.
    /// </summary>
    [Fact]
    public async Task Chat_WithADocumentOfAnotherGame_FindsNothing()
    {
        IndexedPdf elsewhere;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var otherGameId = await TestSessionHelper.SeedSharedGameAsync(db, "Un altro gioco");
            elsewhere = await IndexPdfAsync(db, "elsewhere.pdf", otherGameId, documentType: "base",
                (BothArmsInB, QueryVectorEmbeddings.Axis(1f, 0f)));
        }

        var errors = await AskForErrorsAsync(documentIds: [elsewhere.VectorDocumentId]);

        errors.Should().Equal("NO_RESULTS");
        _hostFixture.Llm.LastUserPrompt.Should().BeNull("senza passaggi il modello non si interroga");
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private sealed record IndexedPdf(Guid PdfId, Guid VectorDocumentId);

    /// <summary>La ricerca vera, via MediatR, senza utente: come la esegue KbQuality.</summary>
    private async Task<List<string>> SearchAsync(string mode, IReadOnlyList<Guid>? documentIds)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        List<SearchResultDto> hits = await mediator.Send(
            new SearchQuery(
                GameId: _gameId,
                Query: Question,
                TopK: 10,
                SearchMode: mode,
                PdfDocumentIds: documentIds),
            Ct);

        return hits.Select(h => h.TextContent).ToList();
    }

    /// <summary>La ricerca fra giochi, con l'utente del test: passaggio e punteggio, nell'ordine.</summary>
    private async Task<List<string>> GlobalSearchAsync(string? language, IReadOnlyList<string>? docType = null)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(
            new GlobalKbSearchQuery(
                Query: Question,
                Limit: 10,
                Cursor: null,
                Mode: SearchMode.Hybrid,
                MinScore: 0.0,
                UserId: _userId,
                Role: UserRole.User,
                DocType: docType,
                Language: language),
            Ct);

        return response.Results
            .Select(r => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{r.Score:F4} {r.Snippet}"))
            .ToList();
    }

    /// <summary>Una domanda che deve ricevere risposta: nessun evento di errore nello stream.</summary>
    private async Task AskAsync(IReadOnlyList<Guid>? documentIds) =>
        (await AskForErrorsAsync(documentIds)).Should().BeEmpty();

    /// <summary>La domanda via HTTP; restituisce i codici degli eventi di errore dello stream.</summary>
    private async Task<List<string?>> AskForErrorsAsync(IReadOnlyList<Guid>? documentIds)
    {
        object payload = documentIds is null
            ? new { gameId = _gameId.ToString(), query = Question }
            : new { gameId = _gameId.ToString(), query = Question, documentIds };
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", _token, payload), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var errors = new List<string?>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(trimmed["data:".Length..].Trim());
            if (doc.RootElement.GetProperty("type").GetInt32() != (int)StreamingEventType.Error) continue;
            errors.Add(doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString());
        }

        return errors;
    }

    /// <summary>
    /// Un PDF pronto, il suo documento vettoriale (con un id diverso da quello del PDF, come in
    /// produzione) e i suoi passaggi nelle due tabelle che i bracci leggono.
    /// </summary>
    private async Task<IndexedPdf> IndexPdfAsync(
        MeepleAiDbContext db, string fileName, Guid gameId, string documentType, params (string Text, float[] Vector)[] chunks)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = fileName,
            FilePath = $"/tmp/{fileName}",
            FileSizeBytes = 1024,
            UploadedByUserId = _userId,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = gameId,
            DocumentType = documentType,
        });

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = gameId,
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
            GameId = gameId,
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
            command.Parameters.AddWithValue("@gameId", gameId);
            command.Parameters.AddWithValue("@text", entities[i].Content);
            command.Parameters.AddWithValue("@vec", new Pgvector.Vector(chunks[i].Vector));
            command.Parameters.AddWithValue("@chunkIndex", i);
            command.Parameters.AddWithValue("@pageNumber", i + 1);
            command.Parameters.AddWithValue("@sourceChunkId", entities[i].Id);
            await command.ExecuteNonQueryAsync(Ct);
        }

        return new IndexedPdf(pdfId, vectorDocumentId);
    }
}
