using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Goldset;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #4288: la valutazione di KbQuality confronta l'id del chunk da cui è nata la domanda con
/// gli id dei risultati del recupero. Prima quei risultati portavano l'id del documento vettoriale
/// (braccio vettoriale) o del PDF (braccio lessicale), mai quello di un chunk: precision e MRR
/// valevano zero qualunque fosse il recupero.
/// </summary>
/// <remarks>
/// Su Postgres vero, perché i tre id divergono solo lì. Il corpus è quello di
/// <see cref="DocumentFilterIdentityTests"/>: nel PDF A un passaggio che trovano entrambi i bracci,
/// uno che trova solo il lessicale e uno che trova solo il vettoriale; il PDF B ne ha uno suo.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KbQuality")]
[Trait("Issue", "4288")]
public sealed class KbQualityChunkIdentityTests
    : IClassFixture<AgentProfileFieldEffectHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";

    private const string BothArmsInA = "At setup each player draws five cards and keeps them hidden.";
    private const string LexicalOnlyInA = "Each player draws two extra cards at setup when the long variant is used.";
    private const string VectorOnlyInA = "Deal a starting hand of five to everybody.";
    private const string BothArmsInB = "In the expansion each player draws seven cards at setup.";

    private readonly AgentProfileFieldEffectHostFixture _hostFixture;
    private Guid _userId;
    private Guid _gameId;
    private IndexedPdf _pdfA = null!;
    private IndexedPdf _pdfB = null!;

    public KbQualityChunkIdentityTests(AgentProfileFieldEffectHostFixture hostFixture)
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

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Coloni della valutazione");

        _pdfA = await IndexPdfAsync(db, "rulebook-a.pdf",
            new Passage(BothArmsInA, QueryVectorEmbeddings.Axis(1f, 0f)),
            new Passage(LexicalOnlyInA, QueryVectorEmbeddings.Axis(0f, 1f)),
            new Passage(VectorOnlyInA, QueryVectorEmbeddings.Axis(1f, 0f)));
        _pdfB = await IndexPdfAsync(db, "expansion-b.pdf",
            new Passage(BothArmsInB, QueryVectorEmbeddings.Axis(1f, 0f)));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>La sonda dell'issue: ogni id restituito è quello di un chunk del PDF cercato.</summary>
    [Fact]
    public async Task TheEvaluationSearch_ReturnsTheIdsOfTheRetrievedChunks()
    {
        var result = await SearchAsync(_pdfA.PdfId);

        result.RetrievedChunkIds.Should().BeEquivalentTo(
            [_pdfA.ChunkIds[BothArmsInA], _pdfA.ChunkIds[LexicalOnlyInA], _pdfA.ChunkIds[VectorOnlyInA]],
            "i due bracci e la loro fusione portano l'id del text_chunks, non quello del documento");
    }

    /// <summary>
    /// Il chunk di origine lo trovano entrambi i bracci, quindi la fusione lo mette primo: la
    /// valutazione deve vederlo al primo posto.
    /// </summary>
    [Fact]
    public async Task AnEvaluation_WhoseSourceChunkIsRetrieved_FindsItAtTheTop()
    {
        var outcome = await EvaluateAsync(_pdfA, sourceChunkId: _pdfA.ChunkIds[BothArmsInA]);

        outcome.Metrics.Precision.At1.Should().Be(1);
        outcome.Metrics.Ranking.Mrr.Should().Be(1);
    }

    /// <summary>
    /// Il controllo: il chunk di origine sta nel PDF B, e la valutazione cerca nel PDF A. Lo zero
    /// qui viene dal recupero, che restituisce id di chunk veri ma non quello (vedi il primo test),
    /// non da un confronto fra id di tabelle diverse.
    /// </summary>
    [Fact]
    public async Task AnEvaluation_WhoseSourceChunkIsNotAmongTheResults_ScoresZero()
    {
        var outcome = await EvaluateAsync(_pdfA, sourceChunkId: _pdfB.ChunkIds[BothArmsInB]);

        outcome.Metrics.Precision.At5.Should().Be(0);
        outcome.Metrics.Ranking.Mrr.Should().Be(0);
    }

    /// <summary>
    /// Un embedding scritto senza <c>source_chunk_id</c> (precedente a SP2, o caricato da
    /// <c>/ingest/pdf</c> finché è aperta #4384) torna senza id, al suo posto: scartarlo farebbe salire
    /// di rango quelli dopo, e la valutazione sembrerebbe migliore di quella che è.
    /// </summary>
    [Fact]
    public async Task AResultFromAnEmbeddingWithoutItsChunk_KeepsItsPlace_WithoutAnId()
    {
        // Nessuna parola della domanda: lo trova solo il braccio vettoriale, l'unico che legge
        // source_chunk_id.
        const string LegacyVectorOnly = "Give a starting hand to everybody at the table.";
        IndexedPdf legacy;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            legacy = await IndexPdfAsync(db, "legacy-c.pdf",
                new Passage(LegacyVectorOnly, QueryVectorEmbeddings.Axis(1f, 0f), LinkedToItsChunk: false));
        }

        var result = await SearchAsync(legacy.PdfId);

        result.RetrievedChunkIds.Should().Equal([null]);
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private sealed record Passage(string Text, float[] Vector, bool LinkedToItsChunk = true);

    private sealed record IndexedPdf(Guid PdfId, IReadOnlyDictionary<string, Guid> ChunkIds);

    /// <summary>La ricerca dall'adattatore vero di KbQuality.</summary>
    private async Task<SearchResult> SearchAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IKbSearchProvider>();
        return await provider.SearchAsync(pdfId, Question, topK: 10, Ct);
    }

    /// <summary>La valutazione vera, su un goldset di una domanda, con la ricerca vera.</summary>
    private async Task<EvaluationOutcome> EvaluateAsync(IndexedPdf pdf, Guid sourceChunkId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IEvaluationExecutor>();
        var snapshot = new PdfDocSnapshot(pdf.PdfId, "rulebook-a.pdf", pdf.ChunkIds.Count, "Ready", []);

        return await executor.ExecuteAsync(
            pdf.PdfId,
            snapshot,
            [new GoldsetQaPair("q1", Question, ExpectedAnswer: "five", SourceChunkId: sourceChunkId)],
            seed: 1,
            Ct);
    }

    /// <summary>
    /// Un PDF pronto, il suo documento vettoriale (con un id diverso da quello del PDF, come in
    /// produzione) e i suoi passaggi nelle due tabelle che i bracci leggono.
    /// </summary>
    private async Task<IndexedPdf> IndexPdfAsync(MeepleAiDbContext db, string fileName, params Passage[] passages)
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
            SharedGameId = _gameId,
        });

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = _gameId,
            PdfDocumentId = pdfId,
            ChunkCount = passages.Length,
            TotalCharacters = passages.Sum(p => p.Text.Length),
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = QueryVectorEmbeddings.Dimensions,
        });

        var chunks = passages.Select((p, i) => new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = _gameId,
            PdfDocumentId = pdfId,
            Content = p.Text,
            ChunkIndex = i,
            PageNumber = i + 1,
            CharacterCount = p.Text.Length,
        }).ToList();
        db.TextChunks.AddRange(chunks);
        await db.SaveChangesAsync(Ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

        for (var i = 0; i < chunks.Count; i++)
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
            command.Parameters.AddWithValue("@gameId", _gameId);
            command.Parameters.AddWithValue("@text", chunks[i].Content);
            command.Parameters.AddWithValue("@vec", new Pgvector.Vector(passages[i].Vector));
            command.Parameters.AddWithValue("@chunkIndex", i);
            command.Parameters.AddWithValue("@pageNumber", i + 1);
            command.Parameters.Add(new NpgsqlParameter("@sourceChunkId", NpgsqlTypes.NpgsqlDbType.Uuid)
            {
                Value = passages[i].LinkedToItsChunk ? chunks[i].Id : DBNull.Value,
            });
            await command.ExecuteNonQueryAsync(Ct);
        }

        return new IndexedPdf(pdfId, chunks.ToDictionary(c => c.Content, c => c.Id, StringComparer.Ordinal));
    }
}
