using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #5003: the two paths that still wrote the text chunks and the embeddings of a PDF in separate commits — the re-index
/// of <see cref="IndexPdfCommandHandler"/> and the upload's <see cref="PdfChunkPersistence"/> — now write them in one
/// transaction, as the queued pipeline does since #4660. Every embedding carries the <c>source_chunk_id</c> of the
/// chunk it comes from, and a new run gives the chunks new ids: a failure between two commits left the tables no longer
/// matching (embeddings pointing at deleted chunks, or a partial index next to the new chunks).
/// </summary>
/// <remarks>
/// Each test indexes a PDF once, which is the "old" index, then runs the same path again with a save of the new
/// embeddings made to fail, and asserts the old chunks and the old embeddings are still there and still agree. Real
/// PostgreSQL, real handler, real unit of work (and so the retrying execution strategy of the test context).
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "5003")]
public sealed class ChunksAndEmbeddingsOneTransactionIntegrationTests : IAsyncLifetime
{
    private const int Dimensions = 768; // the pgvector column is vector(768)

    private readonly SharedTestcontainersFixture _fixture;
    private readonly EmbeddingSaveFailer _failer = new();
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private Guid _sharedGameId;
    private Guid _userId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ChunksAndEmbeddingsOneTransactionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_chunks_one_tx_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.ConfigureDbContext<MeepleAiDbContext>(options => options.AddInterceptors(_failer));

        // The real chunking stack, as in IndexPdfIntegrationTests: the chunk count follows the text.
        services.AddSingleton<ITextChunkingService, TextChunkingService>();
        services.AddScoped<ChunkingStrategySelector>();
        services.AddScoped<IAdvancedChunkingService, AdvancedChunkingService>();
        services.AddScoped(_ => Mock.Of<ISemanticResponseCache>());
        services.AddScoped<IPdfIndexingPipeline, PdfIndexingPipeline>();
        services.AddScoped<ICacheInvalidationRetryPolicy>(_ => new PassthroughRetryPolicy());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository>());

        var embeddings = new Mock<IEmbeddingService>();
        embeddings.Setup(e => e.GetEmbeddingDimensions()).Returns(Dimensions);
        embeddings.Setup(e => e.GetModelName()).Returns("test-embedding-model");
        embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) => new EmbeddingResult
            {
                Success = true,
                Embeddings = texts.Select(_ => Vector()).ToList(),
            });
        services.AddSingleton(embeddings.Object);
        services.AddSingleton(Options.Create(new IndexingSettings { EmbeddingBatchSize = 100 }));
        // VectorDocumentIndexedEvent, published by the pipeline on a successful index, reaches a handler that needs it.
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IndexPdfCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync(Ct);

        _userId = Guid.NewGuid();
        _sharedGameId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = _userId, Email = "chunks@meepleai.dev", DisplayName = "Chunks", Role = "Editor", Tier = "Free", CreatedAt = DateTime.UtcNow,
        });
        db.SharedGames.Add(new SharedGameEntity { Id = _sharedGameId, Title = "One transaction", YearPublished = 2026, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null) await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    /// <summary>
    /// The re-index with more than one batch of 500 embeddings. Before, the first batch committed the new chunks, the
    /// removal of the old embeddings and its own 500; a failure on the second left the new chunks next to a partial
    /// index.
    /// </summary>
    [Fact]
    public async Task Reindex_WhenAnEmbeddingBatchAfterTheFirstFails_TheOldChunksAndEmbeddingsStayAndAgree()
    {
        var pdfId = await CreatePdfAsync(LargeText(paragraphs: 3000));
        var first = await IndexAsync(pdfId);
        first.Success.Should().BeTrue(first.ErrorMessage);
        first.ChunkCount.Should().BeGreaterThan(500, "the failure must hit a batch after the first one");
        var before = await SnapshotAsync(pdfId);

        _failer.FailOnEmbeddingSave = 2;
        var second = await IndexAsync(pdfId);

        second.Success.Should().BeFalse();
        _failer.Tripped.Should().BeTrue("the second batch save was reached and failed");
        var after = await SnapshotAsync(pdfId);
        after.ChunkIds.Should().BeEquivalentTo(before.ChunkIds, "the chunk replacement rolled back with the embeddings");
        after.EmbeddingIds.Should().BeEquivalentTo(before.EmbeddingIds, "no batch of the failed run is left, and the old rows are back");
        after.ShouldAgree();
    }

    /// <summary>
    /// The upload's re-processing. Before, the chunks were a commit of their own: a failure of the embeddings that
    /// followed left the old embeddings pointing at chunks that commit had already deleted.
    /// </summary>
    [Fact]
    public async Task UploadSave_WhenTheEmbeddingsFailAfterTheChunks_TheOldChunksAndEmbeddingsStayAndAgree()
    {
        var pdfId = await CreatePdfAsync("Short text, saved through the upload path.");
        await CreateVectorDocumentAsync(pdfId);
        await UploadSaveAsync(pdfId, Chunks("old", 3));
        var before = await SnapshotAsync(pdfId);
        before.ChunkIds.Should().HaveCount(3);

        _failer.FailOnEmbeddingSave = 1;
        var save = async () => await UploadSaveAsync(pdfId, Chunks("new", 4));

        await save.Should().ThrowAsync<InjectedSaveFailureException>();
        var after = await SnapshotAsync(pdfId);
        after.ChunkIds.Should().BeEquivalentTo(before.ChunkIds, "the chunk save rolled back with the embeddings");
        after.EmbeddingIds.Should().BeEquivalentTo(before.EmbeddingIds);
        after.ShouldAgree();
    }

    /// <summary>
    /// A concurrency conflict keeps its Category B outcome on the re-index (the run fails, the admin mutation wins), and
    /// writes nothing: before, the batches already committed stayed.
    /// </summary>
    [Fact]
    public async Task Reindex_WhenAnEmbeddingBatchHitsAConcurrencyConflict_NothingIsWrittenAndTheRunFails()
    {
        var pdfId = await CreatePdfAsync(LargeText(paragraphs: 3000));
        (await IndexAsync(pdfId)).Success.Should().BeTrue();
        var before = await SnapshotAsync(pdfId);

        _failer.Throw = () => new DbUpdateConcurrencyException("conflict injected by the test");
        _failer.FailOnEmbeddingSave = 2;
        var second = await IndexAsync(pdfId);

        second.Success.Should().BeFalse();
        second.ErrorCode.Should().Be(PdfIndexingErrorCode.VectorIndexingFailed);
        var after = await SnapshotAsync(pdfId);
        after.ChunkIds.Should().BeEquivalentTo(before.ChunkIds);
        after.EmbeddingIds.Should().BeEquivalentTo(before.EmbeddingIds);
        after.ShouldAgree();
    }

    /// <summary>
    /// From the review of #5054: the upload swallowed a conflict and went on to declare the index completed and the PDF
    /// Ready with nothing of the run written. The conflict now leaves the save, after rolling everything back.
    /// </summary>
    [Fact]
    public async Task UploadSave_WhenTheSaveHitsAConcurrencyConflict_ItThrowsAndNothingIsWritten()
    {
        var pdfId = await CreatePdfAsync("Short text, saved through the upload path.");
        await CreateVectorDocumentAsync(pdfId);
        await UploadSaveAsync(pdfId, Chunks("old", 3));
        var before = await SnapshotAsync(pdfId);

        _failer.Throw = () => new DbUpdateConcurrencyException("conflict injected by the test");
        _failer.FailOnEmbeddingSave = 1;
        var save = async () => await UploadSaveAsync(pdfId, Chunks("new", 4));

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var after = await SnapshotAsync(pdfId);
        after.ChunkIds.Should().BeEquivalentTo(before.ChunkIds);
        after.EmbeddingIds.Should().BeEquivalentTo(before.EmbeddingIds);
        after.ShouldAgree();
    }

    /// <summary>
    /// A transient error makes the execution strategy run the transaction again. The second attempt must not trip on
    /// what the first one left in the change tracker: the chunk ids are stable across attempts (an identity conflict),
    /// and the embeddings of the failed batch stay Added (inserted twice). Forgetting them at the head of the delegate is
    /// what this proves.
    /// </summary>
    [Fact]
    public async Task Reindex_WhenAnEmbeddingBatchFailsTransiently_TheRetriedAttemptWritesOneConsistentIndex()
    {
        var pdfId = await CreatePdfAsync(LargeText(paragraphs: 3000));
        (await IndexAsync(pdfId)).Success.Should().BeTrue();
        var before = await SnapshotAsync(pdfId);

        _failer.Throw = Transient;
        _failer.FailOnEmbeddingSave = 2;
        var second = await IndexAsync(pdfId);

        second.Success.Should().BeTrue(second.ErrorMessage);
        _failer.Tripped.Should().BeTrue();
        var after = await SnapshotAsync(pdfId);
        after.ChunkIds.Should().NotIntersectWith(before.ChunkIds, "the retried run replaced the index");
        after.EmbeddingIds.Should().HaveCount(after.ChunkIds.Count, "one embedding per chunk, none left over from the failed attempt");
        after.ShouldAgree();
    }

    [Fact]
    public async Task UploadSave_WhenTheEmbeddingsFailTransiently_TheRetriedAttemptWritesOneConsistentIndex()
    {
        var pdfId = await CreatePdfAsync("Short text, saved through the upload path.");
        await CreateVectorDocumentAsync(pdfId);
        await UploadSaveAsync(pdfId, Chunks("old", 3));
        var before = await SnapshotAsync(pdfId);

        // The classifier may be an LLM call per chunk: it runs before the transaction, so a retry does not repeat it.
        var classifier = new Mock<IRoleClassifierService>();
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkInput> inputs, CancellationToken _) =>
                inputs.Select(_ => GameBookRole.RulesReference).ToList());

        _failer.Throw = Transient;
        _failer.FailOnEmbeddingSave = 1;
        await UploadSaveAsync(pdfId, Chunks("new", 4), classifier.Object);

        _failer.Tripped.Should().BeTrue();
        classifier.Verify(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()), Times.Once(),
            "classified once, before the transaction, not once per attempt");
        var after = await SnapshotAsync(pdfId);
        after.ChunkIds.Should().HaveCount(4).And.NotIntersectWith(before.ChunkIds);
        after.EmbeddingIds.Should().HaveCount(4, "one embedding per chunk, none left over from the failed attempt");
        after.ShouldAgree();
    }

    /// <summary>What the retrying execution strategy treats as transient: an <see cref="NpgsqlException"/> over a timeout.</summary>
    private static Exception Transient() => new NpgsqlException("transient failure injected by the test", new TimeoutException());

    private async Task<IndexingResultDto> IndexAsync(Guid pdfId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IndexPdfCommandHandler>();
        return await handler.Handle(new IndexPdfCommand(pdfId.ToString()), Ct);
    }

    private async Task UploadSaveAsync(Guid pdfId, IReadOnlyList<DocumentChunkInput> chunks, IRoleClassifierService? classifier = null)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = await db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == pdfId, Ct);
        await PdfChunkPersistence.SaveAsync(
            db,
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            pdfId,
            pdf,
            chunks,
            chunks.Select(_ => Vector()).ToList(),
            "test-embedding-model",
            classifier,
            TimeProvider.System,
            NullLogger.Instance,
            nameof(ChunksAndEmbeddingsOneTransactionIntegrationTests),
            Ct);
    }

    private async Task<Guid> CreatePdfAsync(string extractedText)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = _sharedGameId,
            UploadedByUserId = _userId,
            FileName = "one-tx.pdf",
            FilePath = "/test/one-tx.pdf",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow,
            PageCount = 10,
            ExtractedText = extractedText,
        };
        db.PdfDocuments.Add(pdf);
        await db.SaveChangesAsync(Ct);
        return pdf.Id;
    }

    private async Task CreateVectorDocumentAsync(Guid pdfId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Set<VectorDocumentEntity>().Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            GameId = _sharedGameId,
            SharedGameId = _sharedGameId,
            IndexingStatus = "processing",
            EmbeddingModel = "test-embedding-model",
            EmbeddingDimensions = Dimensions,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<IndexSnapshot> SnapshotAsync(Guid pdfId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var chunkIds = await db.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == pdfId)
            .Select(c => c.Id)
            .ToListAsync(Ct);
        var vectorDocId = await db.Set<VectorDocumentEntity>().AsNoTracking()
            .Where(v => v.PdfDocumentId == pdfId)
            .Select(v => v.Id)
            .SingleAsync(Ct);
        var embeddings = await db.PgVectorEmbeddings.AsNoTracking()
            .Where(e => e.VectorDocumentId == vectorDocId)
            .Select(e => new { e.Id, e.SourceChunkId })
            .ToListAsync(Ct);
        return new IndexSnapshot(chunkIds, embeddings.Select(e => e.Id).ToList(), embeddings.Select(e => e.SourceChunkId).ToList());
    }

    private static List<DocumentChunkInput> Chunks(string tag, int count) => Enumerable.Range(0, count)
        .Select(i => new DocumentChunkInput { Text = $"{tag} chunk {i}", Page = 1, CharStart = i * 10, CharEnd = i * 10 + 9 })
        .ToList();

    private static string LargeText(int paragraphs) => string.Join("\n\n", Enumerable.Range(1, paragraphs)
        .Select(i => $"Paragraph {i}: " + string.Join(" ", Enumerable.Range(1, 50).Select(w => $"Word{w}"))));

    private static float[] Vector() => Enumerable.Range(0, Dimensions).Select(i => i * 0.001f).ToArray();

    private sealed record IndexSnapshot(List<Guid> ChunkIds, List<Guid> EmbeddingIds, List<Guid?> SourceChunkIds)
    {
        /// <summary>Every embedding points at a chunk of the PDF that exists: the two tables still correspond.</summary>
        public void ShouldAgree()
        {
            EmbeddingIds.Should().NotBeEmpty();
            SourceChunkIds.Should().OnlyContain(id => id.HasValue && ChunkIds.Contains(id.Value),
                "every embedding must point at a text chunk that is still there");
        }
    }

    private sealed class InjectedSaveFailureException() : Exception("save of the new embeddings refused by the test");

    /// <summary>
    /// Fails the N-th save that carries new pgvector rows, once armed (0 = disarmed). Counting only saves with Added
    /// embeddings is what places the failure after the text chunks: in the re-index the chunks go with the first batch,
    /// in the upload they have a save of their own before it.
    /// </summary>
    private sealed class EmbeddingSaveFailer : SaveChangesInterceptor
    {
        private int _embeddingSaves;
        public volatile int FailOnEmbeddingSave;
        public volatile bool Tripped;
        public Func<Exception> Throw = () => new InjectedSaveFailureException();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailOnEmbeddingSave > 0
                && eventData.Context!.ChangeTracker.Entries<PgVectorEmbeddingEntity>().Any(e => e.State == EntityState.Added)
                && Interlocked.Increment(ref _embeddingSaves) == FailOnEmbeddingSave)
            {
                Tripped = true;
                throw Throw();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
