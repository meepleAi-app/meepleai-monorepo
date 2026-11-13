using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4660 on a real Postgres: the text chunks of a run and the embeddings that point at them are written in ONE
/// transaction, so a run that fails while writing the embeddings leaves the previous chunks AND the previous
/// embeddings, which still agree.
/// </summary>
/// <remarks>
/// <para>
/// The pgvector rows carry <c>source_chunk_id</c> and <c>role_tags</c> of the text chunk they come from, and the chunks
/// of a re-processed PDF get new ids. The pipeline used to save the chunks first (committed) and replace the embeddings
/// after: a COPY that failed left the old embeddings — the only ones the index has, since the replacement keeps them
/// when it fails — pointing at chunks the first write had already deleted.
/// </para>
/// <para>
/// The COPY fails here with a vector of the wrong width for the column, which the server rejects while it receives the
/// rows: after the DELETE, and after the chunks were written. Everything is the production wiring but the services
/// outside the database (extraction, chunking, embedding, storage): the unit of work with the retrying execution
/// strategy, the indexing pipeline, the pgvector adapter, one scoped context shared by all of them as in the queue job.
/// </para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4660")]
public sealed class PdfPipelineAtomicChunkIndexIntegrationTests : IAsyncLifetime
{
    private const int ColumnWidth = 768; // vector(768): the width the table is born with

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _provider;
    private Guid _userId;
    private string _userEmail = string.Empty;
    private Guid _sharedGameId;
    private Guid _pdfId;

    // What the services outside the database answer; the tests change the width to make the COPY fail.
    private int _embeddingWidth = ColumnWidth;

    // What the pipeline logged in its last run: the reason a run that did not end as the test expects, in the failure message.
    private CapturingLogger<PdfProcessingPipelineService> _pipelineLog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PdfPipelineAtomicChunkIndexIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_atomic_chunks_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IVectorStoreAdapter, PgVectorStoreAdapter>();
        services.AddScoped(_ => Mock.Of<ISemanticResponseCache>());
        // The completion of the vector document publishes an event whose handlers (the KB flag, the projections) are not
        // what this test is about, and need a wiring of their own.
        services.AddScoped(_ => Mock.Of<IMediator>());
        services.AddScoped<IPdfIndexingPipeline, PdfIndexingPipeline>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await TestMigrationHelper.MigrateWithRetryAsync(db, Ct);

        _userId = Guid.NewGuid();
        _userEmail = $"atomic-chunks-{_userId:N}@test.local";
        _sharedGameId = Guid.NewGuid();
        _pdfId = Guid.NewGuid();
        db.Users.Add(new UserEntity { Id = _userId, Email = _userEmail, CreatedAt = DateTime.UtcNow });
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = _sharedGameId,
            Title = $"Gioco {_sharedGameId:N}",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 2,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
        });
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfId,
            SharedGameId = _sharedGameId,
            FileName = "rules.pdf",
            FilePath = $"pdf/{_sharedGameId:N}/{_pdfId:N}_rules.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Pending",
        });
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                /* best-effort */
            }
        }
    }

    [Fact]
    public async Task FirstRun_SavesTheChunksAndTheEmbeddingsThatPointAtThem()
    {
        var outcome = await ProcessAsync();

        outcome.Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        var chunks = await ChunksAsync();
        var embeddings = await EmbeddingsAsync();
        chunks.Should().HaveCount(3);
        embeddings.Should().HaveCount(3);
        embeddings.Select(e => e.SourceChunkId).Should().BeEquivalentTo(chunks.Select(c => (Guid?)c.Id),
            "every embedding points at the chunk it comes from");
        foreach (var embedding in embeddings)
        {
            var chunk = chunks.Single(c => c.ChunkIndex == embedding.ChunkIndex);
            embedding.SourceChunkId.Should().Be(chunk.Id);
            embedding.RoleTags.Should().Be((int)chunk.RoleTags).And.NotBe(0, "the role is denormalized onto the row");
        }

        (await VectorDocumentStatusAsync()).Should().Be("completed");
    }

    /// <summary>
    /// The defect. The second run replaces the chunks (new ids) and then its COPY fails: with the chunks committed
    /// first, the old embeddings were left pointing at chunks that no longer exist.
    /// </summary>
    [Fact]
    public async Task Reprocess_WhenTheEmbeddingCopyFails_KeepsTheOldChunksAndTheOldEmbeddingsTogether()
    {
        (await ProcessAsync()).Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        var oldChunkIds = (await ChunksAsync()).Select(c => c.Id).ToList();
        var oldEmbeddingIds = (await EmbeddingsAsync()).Select(e => e.Id).ToList();
        await BackToPendingAsync();
        _embeddingWidth = 3;

        var outcome = await ProcessAsync();

        outcome.Should().Be(PdfPipelineOutcome.Failed, "{0}", _pipelineLog.Problems);
        (await PdfAsync()).FailedAtState.Should().Be("Indexing");
        _pipelineLog.Problems.Should().Contain("768 dimensions",
            "the run has to fail on the server rejecting the vectors, at the end of the COPY and after the chunks were written, or the rest proves nothing");
        (await ChunksAsync()).Select(c => c.Id).Should().BeEquivalentTo(oldChunkIds,
            "the chunk replacement was in the transaction that rolled back");
        var embeddings = await EmbeddingsAsync();
        embeddings.Select(e => e.Id).Should().BeEquivalentTo(oldEmbeddingIds, "the replacement keeps the old embeddings when it fails");
        embeddings.Select(e => e.SourceChunkId).Should().BeEquivalentTo(oldChunkIds.Select(id => (Guid?)id),
            "and they still point at chunks that exist");
        (await VectorDocumentStatusAsync()).Should().Be("completed", "a re-run of an indexed document leaves it as it was");
    }

    [Fact]
    public async Task Reprocess_WhenItSucceeds_ReplacesTheChunksAndTheEmbeddingsTogether()
    {
        (await ProcessAsync()).Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        var oldChunkIds = (await ChunksAsync()).Select(c => c.Id).ToList();
        var oldEmbeddingIds = (await EmbeddingsAsync()).Select(e => e.Id).ToList();
        await BackToPendingAsync();

        var outcome = await ProcessAsync();

        outcome.Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        var chunks = await ChunksAsync();
        var embeddings = await EmbeddingsAsync();
        chunks.Select(c => c.Id).Should().NotIntersectWith(oldChunkIds, "the second run built its own chunks");
        chunks.Should().HaveCount(3);
        embeddings.Should().HaveCount(3);
        embeddings.Select(e => e.Id).Should().NotIntersectWith(oldEmbeddingIds);
        embeddings.Select(e => e.SourceChunkId).Should().BeEquivalentTo(chunks.Select(c => (Guid?)c.Id));
    }

    /// <summary>
    /// The replacement is inside the unit of work, not just next to it. The COPY here has succeeded; what fails is the
    /// save that closes the unit of work, after it. If the adapter ran its DELETE and COPY on a transaction of its own
    /// the new embeddings would already be committed, and only the chunks would go back.
    /// </summary>
    [Fact]
    public async Task Reprocess_WhenTheUnitOfWorkFailsAfterTheReplacement_TheReplacementIsRolledBackToo()
    {
        (await ProcessAsync()).Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        var oldChunkIds = (await ChunksAsync()).Select(c => c.Id).ToList();
        var oldEmbeddingIds = (await EmbeddingsAsync()).Select(e => e.Id).ToList();
        await BackToPendingAsync();
        var replaced = 0;

        var outcome = await ProcessAsync(afterTheReplacement: db =>
        {
            replaced++;
            // A second user with the same email: the unit of work's own save fails on the unique index.
            db.Users.Add(new UserEntity { Id = Guid.NewGuid(), Email = _userEmail, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        });

        outcome.Should().Be(PdfPipelineOutcome.Failed, "{0}", _pipelineLog.Problems);
        replaced.Should().Be(1, "the replacement has to have run, or the rollback proves nothing");
        _pipelineLog.Problems.Should().Contain("duplicate key", "the run has to fail on the save that closes the unit of work");
        (await ChunksAsync()).Select(c => c.Id).Should().BeEquivalentTo(oldChunkIds);
        var embeddings = await EmbeddingsAsync();
        embeddings.Select(e => e.Id).Should().BeEquivalentTo(oldEmbeddingIds, "the DELETE and the COPY went back with the chunks");
        embeddings.Select(e => e.SourceChunkId).Should().BeEquivalentTo(oldChunkIds.Select(id => (Guid?)id));
    }

    /// <summary>
    /// The execution strategy of the context (the production wiring: <c>EnableRetryOnFailure</c>) runs the unit of work
    /// again after a transient failure. The first attempt got as far as saving the chunks and replacing the embeddings
    /// before it failed; both went back, and the second attempt starts from the tracker that attempt left.
    /// </summary>
    [Fact]
    public async Task Reprocess_WhenTheFirstAttemptFailsTransiently_TheStrategyRerunsItAndTheSecondSucceeds()
    {
        (await ProcessAsync()).Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        var oldChunkIds = (await ChunksAsync()).Select(c => c.Id).ToList();
        var oldEmbeddingIds = (await EmbeddingsAsync()).Select(e => e.Id).ToList();
        await BackToPendingAsync();
        var attempts = 0;

        var outcome = await ProcessAsync(afterTheReplacement: _ =>
        {
            attempts++;
            return attempts == 1 ? Task.FromException(new TimeoutException("a transient failure")) : Task.CompletedTask;
        });

        outcome.Should().Be(PdfPipelineOutcome.Processed, "{0}", _pipelineLog.Problems);
        attempts.Should().Be(2, "the strategy has to have run the unit of work a second time");
        var chunks = await ChunksAsync();
        var embeddings = await EmbeddingsAsync();
        chunks.Should().HaveCount(3);
        chunks.Select(c => c.Id).Should().NotIntersectWith(oldChunkIds, "the old chunks are replaced, once");
        embeddings.Should().HaveCount(3);
        embeddings.Select(e => e.Id).Should().NotIntersectWith(oldEmbeddingIds);
        embeddings.Select(e => e.SourceChunkId).Should().BeEquivalentTo(chunks.Select(c => (Guid?)c.Id));
    }

    // ─── Support ────────────────────────────────────────────────────────────────

    /// <summary>One run of the queue pipeline, in one scope: the context is shared by every collaborator, as in the job.</summary>
    private async Task<PdfPipelineOutcome> ProcessAsync(Func<MeepleAiDbContext, Task>? afterTheReplacement = null)
    {
        _pipelineLog = new CapturingLogger<PdfProcessingPipelineService>();
        using var scope = _provider!.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MeepleAiDbContext>();

        var extractor = new Mock<IPdfTextExtractor>();
        extractor
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedTextExtractionResult.CreateSuccess(
                pageChunks: Enumerable.Range(1, 3)
                    .Select(p => new PageTextChunk(PageNumber: p, Text: $"chunk{p} text", CharStartIndex: (p - 1) * 50, CharEndIndex: p * 50))
                    .ToList(),
                totalPages: 3,
                totalCharacters: 36,
                ocrTriggered: false,
                structuredElements: [new ExtractedElement("Setup", 1, "Title"), new ExtractedElement("Place the tiles.", 1, "NarrativeText")]));
        var blobs = new Mock<IBlobStorageService>();
        blobs
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([0x25, 0x50, 0x44, 0x46]));
        var chunking = new Mock<IAdvancedChunkingService>();
        chunking
            .Setup(s => s.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => NewChunks());
        var embeddings = new Mock<IEmbeddingService>();
        embeddings
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) => new EmbeddingResult
            {
                Success = true,
                Embeddings = texts.Select(_ => NewVector(_embeddingWidth)).ToList(),
            });
        embeddings.Setup(e => e.GetModelName()).Returns("test-embedding-model");
        var language = new Mock<ILanguageDetector>();
        language.Setup(l => l.Detect(It.IsAny<string>())).Returns(new LanguageDetectionResult("en", true, 0.95));
        var roles = new Mock<IRoleClassifierService>();
        var cycle = new[] { GameBookRole.Setup, GameBookRole.RulesReference, GameBookRole.Lore };
        roles
            .Setup(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkInput> inputs, CancellationToken _) =>
                (IReadOnlyList<GameBookRole>)inputs.Select((_, i) => cycle[i % cycle.Length]).ToList());

        var vectorStore = sp.GetRequiredService<IVectorStoreAdapter>();
        if (afterTheReplacement is not null)
        {
            // The real adapter, with a step run right after the replacement and still inside the unit of work: the
            // way to fail something AFTER the DELETE and the COPY have run.
            var real = vectorStore;
            var spy = new Mock<IVectorStoreAdapter>();
            spy.Setup(s => s.EnsureCollectionExistsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((Guid gameId, int width, CancellationToken ct) => real.EnsureCollectionExistsAsync(gameId, width, ct));
            spy.Setup(s => s.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid id, List<Embedding> rows, CancellationToken ct) =>
                {
                    await real.ReplaceByVectorDocumentIdAsync(id, rows, ct);
                    await afterTheReplacement(db);
                });
            vectorStore = spy.Object;
        }

        var pipeline = new PdfProcessingPipelineService(
            db,
            new RelationalPdfClaimService(db),
            extractor.Object,
            Mock.Of<ITextChunkingService>(),
            embeddings.Object,
            blobs.Object,
            TimeProvider.System,
            _pipelineLog,
            language.Object,
            Mock.Of<IChunkTranslationService>(),
            sp.GetRequiredService<IPdfIndexingPipeline>(),
            sp.GetRequiredService<IUnitOfWork>(),
            vectorStore: vectorStore,
            roleClassifier: roles.Object,
            advancedChunking: chunking.Object);

        return await pipeline.ProcessAsync(_pdfId, $"pdf/{_sharedGameId:N}/{_pdfId:N}_rules.pdf", _userId, Ct);
    }

    /// <summary>Fresh chunks, with fresh ids, on every call: a re-processed PDF never gets its old chunk ids back.</summary>
    private List<HierarchicalChunk> NewChunks()
    {
        var parent = HierarchicalChunk.CreateParent(
            "Setup section content",
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = _pdfId });
        var child1 = HierarchicalChunk.CreateChild(
            "Place the tiles on the board.",
            level: 2,
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfId },
            parent.Id);
        var child2 = HierarchicalChunk.CreateChild(
            "Each player draws three cards.",
            level: 2,
            new ChunkMetadata { Page = 2, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfId },
            parent.Id);
        return [parent, child1, child2];
    }

    private static float[] NewVector(int width)
    {
        var vector = new float[width];
        vector[0] = 1f;
        return vector;
    }

    /// <summary>What a reindex does to the PDF before the queue picks it up again.</summary>
    private async Task BackToPendingAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.AsTracking().SingleAsync(p => p.Id == _pdfId, Ct);
        pdf.ProcessingState = "Pending";
        await db.SaveChangesAsync(Ct);
    }

    private async Task<PdfDocumentEntity> PdfAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfId, Ct);
    }

    private async Task<List<TextChunkEntity>> ChunksAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.TextChunks.AsNoTracking().Where(c => c.PdfDocumentId == _pdfId).OrderBy(c => c.ChunkIndex).ToListAsync(Ct);
    }

    private async Task<string?> VectorDocumentStatusAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.VectorDocuments.AsNoTracking()
            .Where(v => v.PdfDocumentId == _pdfId)
            .Select(v => v.IndexingStatus)
            .SingleOrDefaultAsync(Ct);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly List<string> _entries = [];

        public string Problems => string.Join(Environment.NewLine, _entries);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                _entries.Add($"{logLevel}: {formatter(state, exception)}{(exception is null ? string.Empty : " | " + exception)}");
            }
        }
    }

    private sealed record EmbeddingRow(Guid Id, Guid? SourceChunkId, int ChunkIndex, int RoleTags);

    private async Task<List<EmbeddingRow>> EmbeddingsAsync()
    {
        using var scope = _provider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var vectorDocumentId = await db.VectorDocuments.AsNoTracking()
            .Where(v => v.PdfDocumentId == _pdfId)
            .Select(v => v.Id)
            .SingleAsync(Ct);
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(Ct);
        }

        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = """
            SELECT id, source_chunk_id, chunk_index, role_tags
            FROM pgvector_embeddings
            WHERE vector_document_id = @vectorDocumentId
            ORDER BY chunk_index
            """;
        command.Parameters.AddWithValue("@vectorDocumentId", vectorDocumentId);

        var rows = new List<EmbeddingRow>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            rows.Add(new EmbeddingRow(
                reader.GetGuid(0),
                await reader.IsDBNullAsync(1, Ct) ? null : reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return rows;
    }
}
