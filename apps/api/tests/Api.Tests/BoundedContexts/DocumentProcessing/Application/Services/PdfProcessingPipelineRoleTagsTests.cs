using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Regression guard for the pipeline-ordering bug (B3): <see cref="PdfProcessingPipelineService"/>
/// used to call <c>IndexInVectorStoreAsync</c> BEFORE <c>SaveTextChunksAsync</c>. The vector-store
/// indexing step denormalizes <c>role_tags</c> + <c>source_chunk_id</c> onto the pgvector rows by
/// reading the freshly-saved <c>text_chunks</c> (keyed by <c>ChunkIndex</c>) — but with the old
/// ordering those rows did not exist yet, so every pgvector row was born with
/// <c>RoleTags == 0</c> (None) and <c>SourceChunkId == null</c>. That silently disabled the
/// vector-arm role-match boost (<c>FusionSignals.ComputeRoleMatchBoost</c>) corpus-wide.
///
/// This test captures the <see cref="Embedding"/> batch handed to
/// <see cref="IVectorStoreAdapter.ReplaceByVectorDocumentIdAsync"/> — the correct seam (NOT EF PgVectorEmbeddings) —
/// and asserts each row's <c>RoleTags</c> / <c>SourceChunkId</c> match the persisted
/// <see cref="TextChunkEntity"/> at the same <c>ChunkIndex</c>. RED before the swap (all 0 / null),
/// GREEN after.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingPipelineRoleTagsTests : IDisposable
{
    private readonly FailOnDemandSaveInterceptor _failingSaves = new();
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IPdfTextExtractor> _pdfTextExtractorMock = new();
    private readonly Mock<IPdfTableExtractor> _tableExtractorMock = new();
    private readonly Mock<ITextChunkingService> _chunkingServiceMock = new();
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();
    private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
    private readonly Mock<IAdvancedChunkingService> _advancedChunkingMock = new();
    private readonly Mock<IChunkTranslationService> _chunkTranslationServiceMock = new();
    private readonly Mock<IRoleClassifierService> _roleClassifierMock = new();
    private readonly Mock<IVectorStoreAdapter> _vectorStoreMock = new();
    private readonly Mock<IPdfIndexingPipeline> _indexingPipelineMock = new();

    private readonly Guid _pdfDocumentId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public PdfProcessingPipelineRoleTagsTests()
    {
        // Same as TestDbContextFactory.CreateInMemoryDbContext, plus an interceptor that fails a save on demand
        // (disarmed unless a test arms it).
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_failingSaves)
            .ConfigureWarnings(warnings =>
            {
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<MediatR.IMediator>().Object, TestDbContextFactory.CreateMockEventCollector().Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessAsync_DenormalizesRoleTagsAndSourceChunkId_OntoPgvectorRows()
    {
        // Arrange
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();

        List<Embedding>? captured = null;
        Guid? replacedVectorDocumentId = null;
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .Callback((Guid vectorDocumentId, List<Embedding> batch, CancellationToken __) =>
            {
                replacedVectorDocumentId = vectorDocumentId;
                captured = batch;
            })
            .Returns(Task.CompletedTask);

        var sut = CreatePipeline();

        // Act
        await sut.ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        // Assert: pipeline ran to completion (guards against a vacuous pass on an early failure).
        var doc = await _db.PdfDocuments.FindAsync(_pdfDocumentId);
        doc!.ProcessingState.Should().Be("Ready");

        captured.Should().NotBeNull("the pipeline must hand the pgvector rows to IVectorStoreAdapter.ReplaceByVectorDocumentIdAsync");

        // The id to delete is the vector document's, not the PDF's: with the PDF's id nothing would be deleted
        // and the embeddings of every re-index would pile up.
        var vectorDocument = await _db.VectorDocuments.SingleAsync(v => v.PdfDocumentId == _pdfDocumentId);
        replacedVectorDocumentId.Should().Be(vectorDocument.Id);
        replacedVectorDocumentId.Should().NotBe(_pdfDocumentId);
        captured!.Should().OnlyContain(e => e.VectorDocumentId == vectorDocument.Id);

        var persistedByIndex = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == _pdfDocumentId)
            .ToDictionaryAsync(tc => tc.ChunkIndex);

        captured!.Should().HaveCount(persistedByIndex.Count);

        // The classifier tagged every chunk with a NON-None role. Each pgvector row must carry the
        // SAME role + reference the persisted text_chunk id, matched by ChunkIndex. Pre-fix the
        // denormalization lookup was empty (text_chunks not saved yet) so all rows were 0 / null.
        foreach (var emb in captured!)
        {
            persistedByIndex.Should().ContainKey(emb.ChunkIndex);
            var tc = persistedByIndex[emb.ChunkIndex];
            emb.RoleTags.Should().Be((int)tc.RoleTags,
                "each pgvector row must denormalize the persisted text_chunk role_tags at the same ChunkIndex");
            emb.SourceChunkId.Should().Be(tc.Id,
                "each pgvector row must reference the persisted text_chunk id at the same ChunkIndex");
        }

        // Explicit bug signal: no row may be left at the default (roleTags=0 / sourceChunkId=null).
        captured!.Should().OnlyContain(e => e.RoleTags != 0);
        captured!.Should().OnlyContain(e => e.SourceChunkId != null);
    }

    /// <summary>
    /// #4609 — the vector document is declared <c>completed</c> (and has_knowledge_base raised) LAST, after the
    /// pgvector rows are saved: <c>BeginIndexingAsync</c> first, so the row exists, then the rows, then
    /// <c>IndexAsync</c>.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CompletesTheVectorDocumentOnlyAfterTheEmbeddingsAreSaved()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();

        var calls = new List<string>();
        _indexingPipelineMock
            .Setup(p => p.BeginIndexingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("begin"))
            .Returns(async (Guid docId, Guid? gid, Guid? sgid, int chunkCount, int totalChars, string lang, CancellationToken ct) =>
            {
                _db.VectorDocuments.Add(new VectorDocumentEntity
                {
                    Id = Guid.NewGuid(), PdfDocumentId = docId, GameId = gid, SharedGameId = sgid,
                    ChunkCount = chunkCount, TotalCharacters = totalChars, IndexingStatus = "processing"
                });
                await _db.SaveChangesAsync(ct);
            });
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("embeddings"))
            .Returns(Task.CompletedTask);
        _indexingPipelineMock
            .Setup(p => p.IndexAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("complete"))
            .Returns(Task.CompletedTask);

        await CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None);

        (await _db.PdfDocuments.FindAsync(_pdfDocumentId))!.ProcessingState.Should().Be("Ready", "a run that stopped early would make the order below vacuous");
        calls.Should().Equal(new[] { "begin", "embeddings", "complete" });
    }

    /// <summary>
    /// #4609 — if saving the pgvector rows fails, the document is never declared <c>completed</c>: no
    /// <c>IndexAsync</c>, so no <c>VectorDocumentIndexedEvent</c> and no has_knowledge_base on an index that
    /// does not exist.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenSavingTheEmbeddingsFails_NeverCompletesTheVectorDocument()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the vector store is down"));

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        _indexingPipelineMock.Verify(
            p => p.BeginIndexingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _indexingPipelineMock.Verify(
            p => p.IndexAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Seguito di #4609: se il salvataggio degli embedding fallisce e il PDF finisce `Failed`, anche il suo documento
    /// vettoriale (nato `processing` da Begin) viene chiuso come fallito, invece di restare in elaborazione.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenSavingTheEmbeddingsFails_MarksTheVectorDocumentFailed()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the vector store is down"));

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        _vectorStoreMock.Verify(
            v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "the failure has to come from saving the embeddings, not from a step before it");
        _indexingPipelineMock.Verify(
            p => p.MarkIndexingFailedAsync(
                _pdfDocumentId,
                It.Is<string>(m => m.StartsWith("Processing failed (", StringComparison.Ordinal)
                    && !m.Contains("the vector store is down", StringComparison.Ordinal)),
                CancellationToken.None),
            Times.Once,
            "the vector document gets the category, not the exception message: pdf-status serves that column to users");
    }

    /// <summary>
    /// Seguito di #4609, dalla revisione di #4657: `IndexAsync` scrive `completed` nell'entità tracciata PRIMA di
    /// salvare. Se il salvataggio fallisce, la riga resta `Modified` nel contesto, che questa pipeline condivide con
    /// il gestore d'errore; il salvataggio del PDF `Failed` rimanderebbe quel `completed` al database. Un PDF
    /// fallito non può lasciare un documento vettoriale `completed`.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenTheFailureLeavesTheVectorRowCompletedInTheTracker_TheDatabaseNeverSeesIt()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var tracked = _db.VectorDocuments.Local.Single();
                tracked.IndexingStatus = "completed";
                tracked.IndexedAt = DateTime.UtcNow;
            })
            .ThrowsAsync(new InvalidOperationException("the vector store is down"));

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        (await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId)).ProcessingState
            .Should().Be("Failed", "a run that did not fail would make the check below vacuous");
        (await _db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == _pdfDocumentId)).IndexingStatus
            .Should().NotBe("completed", "the completion never reached the database before the save that failed");
    }

    /// <summary>
    /// PdfProcessingQuartzJob condivide questo contesto (la pipeline è scoped come il suo DbContext) e dopo il
    /// ritorno della pipeline salva il fallimento del job e dei suoi passi attraverso di esso. Il gestore d'errore
    /// non deve staccare quelle entità: un <c>ChangeTracker.Clear()</c> farebbe del suo salvataggio un no-op e
    /// lascerebbe il job in <c>Processing</c>, a occupare uno dei posti dei worker. Né deve scartare una modifica che il
    /// job ha in sospeso su di esse: non è un residuo del guasto della pipeline.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenTheFailureIsHandled_LeavesTheEntitiesOfTheSharedContextTracked()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        var job = new Api.Infrastructure.Entities.DocumentProcessing.ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = _pdfDocumentId,
            UserId = Guid.NewGuid(),
            Status = "Processing",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var step = new Api.Infrastructure.Entities.DocumentProcessing.ProcessingStepEntity
        {
            Id = Guid.NewGuid(),
            ProcessingJobId = job.Id,
            StepName = "Embed",
            Status = "Pending",
        };
        _db.ProcessingJobs.Add(job);
        _db.ProcessingSteps.Add(step);
        await _db.SaveChangesAsync();
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            // A change the job holds on the shared context, made after the pipeline's last save: still unsaved when the
            // failure handler runs (a change made earlier would be flushed by the pipeline's own saves).
            .Callback(() =>
            {
                job.CurrentStep = "Embed";
                step.Status = "Running";
            })
            .ThrowsAsync(new InvalidOperationException("the vector store is down"));

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        (await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId)).ProcessingState
            .Should().Be("Failed", "a run that did not fail would make the check below vacuous");
        _db.Entry(job).State.Should().NotBe(
            EntityState.Detached, "the job that shares this context saves its own failure through its tracked entity");
        job.CurrentStep.Should().Be("Embed", "the change the job holds is not a residue of the pipeline's failure");
        step.Status.Should().Be("Running", "nor is the one on its step");
    }

    /// <summary>
    /// #4660 punto 5. Il salvataggio dei chunk di testo fallisce: restano `Added` nel tracker, e il salvataggio che
    /// segna il PDF `Failed` (un SaveChanges sull'intero contesto) li rimanderebbe e fallirebbe uguale, inghiottito dal
    /// catch: il PDF non diventerebbe mai `Failed` e il suo documento vettoriale non sarebbe chiuso.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenSavingTheTextChunksFails_ThePdfIsStillMarkedFailed()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        _failingSaves.FailWhen = context => context.ChangeTracker.Entries<TextChunkEntity>().Any(e => e.State == EntityState.Added);

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        (await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId)).ProcessingState
            .Should().Be("Failed");
        _failingSaves.Failures.Should().Be(1, "the failure has to come from the text-chunk save, and only from it");
    }

    /// <summary>
    /// #4660 punto 5, il caso del reprocess: i chunk vecchi del PDF sono segnati `Deleted` e i nuovi `Added` in un
    /// solo salvataggio, che fallisce. Il salvataggio che segna il PDF `Failed` non deve cancellare i vecchi chunk,
    /// che nel database sono ancora l'unico testo indicizzabile di quel documento.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenSavingTheNewTextChunksFails_TheOldChunksOfTheReprocessedPdfSurvive()
    {
        SeedPdfDocument();
        for (var index = 0; index < 2; index++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = _pdfDocumentId,
                PrivateGameId = _gameId,
                Content = "old chunk",
                ChunkIndex = index,
                PageNumber = 1,
                CharacterCount = 9,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking; // PERF-06, as in production
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        var oldChunksDeletedAtTheFailure = false;
        _failingSaves.FailWhen = context =>
        {
            var failing = context.ChangeTracker.Entries<TextChunkEntity>().Any(e => e.State == EntityState.Added);
            oldChunksDeletedAtTheFailure |= failing
                && context.ChangeTracker.Entries<TextChunkEntity>().Any(e => e.State == EntityState.Deleted);
            return failing;
        };

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        var reprocessed = await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId);
        reprocessed.ProcessingState.Should().Be("Failed", "a run that did not fail would make the check below vacuous");
        _failingSaves.Failures.Should().Be(1, "the failure has to come from the save of the new chunks, and only from it (the PDF failed with: " + reprocessed.ProcessingError + ")");
        oldChunksDeletedAtTheFailure.Should().BeTrue("the old chunks have to be pending deletion in the failed save, or the check below proves nothing");
        (await _db.TextChunks.AsNoTracking().CountAsync(c => c.PdfDocumentId == _pdfDocumentId && c.Content == "old chunk"))
            .Should().Be(2, "the save that deleted them is the one that failed");
    }

    /// <summary>
    /// #4660 punto 5. Il salvataggio finale (`Ready`) fallisce senza essere un conflitto di concorrenza: il PDF resta
    /// `Ready` solo in memoria, e il controllo «non sovrascrivere un PDF Ready» del gestore d'errore leggeva il
    /// tracker, quindi saltava la scrittura di `Failed`: nel database il PDF restava nell'ultimo stato salvato.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenTheFinalSaveFails_ThePdfIsStillMarkedFailed()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        _failingSaves.FailWhen = context => context.ChangeTracker.Entries<PdfDocumentEntity>()
            .Any(e => e.State == EntityState.Modified && string.Equals(e.Entity.ProcessingState, "Ready", StringComparison.Ordinal));

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        (await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId)).ProcessingState
            .Should().Be("Failed");
        _failingSaves.Failures.Should().Be(1, "the failure has to come from the final Ready save, and only from it");
        (await _db.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == _pdfDocumentId)).FailedAtState
            .Should().Be("Indexing", "the state recorded is the last one saved, not the Ready that only reached memory");
    }

    /// <summary>
    /// Il salvataggio di `Failed` perde una gara di concorrenza (la riga è cambiata da altri): vince l'altro, ma il PDF
    /// non deve restare `Modified` nel tracker con il vecchio `xmin`. <c>PdfProcessingQuartzJob</c> condivide il
    /// contesto e salva il fallimento del suo job subito dopo: quel salvataggio ritroverebbe la stessa riga vecchia,
    /// fallirebbe uguale e il job resterebbe `Processing`.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_WhenTheFailedSaveLosesAConcurrencyRace_LeavesNothingPendingForTheJobsSave()
    {
        SeedPdfDocument();
        SetupExtractor(pageCount: 3);
        SetupBlobStorage();
        SetupHierarchicalChunking();
        SetupEmbeddings();
        SetupRoleClassifierDistinctPerChunk();
        SetupIndexingPipelineCreatesVectorDocument();
        _vectorStoreMock
            .Setup(v => v.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the vector store is down"));
        _failingSaves.ExceptionToThrow = () => new DbUpdateConcurrencyException("xmin conflict");
        _failingSaves.FailWhen = context => context.ChangeTracker.Entries<PdfDocumentEntity>()
            .Any(e => e.State == EntityState.Modified && string.Equals(e.Entity.ProcessingState, "Failed", StringComparison.Ordinal));

        await Record.ExceptionAsync(() => CreatePipeline().ProcessAsync(_pdfDocumentId, "/fake/path.pdf", Guid.NewGuid(), CancellationToken.None));

        _failingSaves.Failures.Should().Be(1, "the conflict has to hit the save that marks the PDF Failed, and only it");
        _db.ChangeTracker.HasChanges().Should().BeFalse("a stale write left in the tracker would fail the job's own save as well");
    }

    /// <summary>
    /// What <c>DiscardUnsavedChanges</c> relies on: a Modified entity put back to Unchanged gets the values it last had
    /// in the database (loaded or saved), with no explicit copy and no database access. If a future EF Core stopped doing it, the discarded
    /// PDF would keep its in-memory <c>Ready</c> and the failure handler would skip it.
    /// </summary>
    [Fact]
    public async Task EfCore_AModifiedEntityPutBackToUnchanged_HasItsLastSavedValues()
    {
        SeedPdfDocument();
        _db.ChangeTracker.Clear();
        var pdf = await _db.PdfDocuments.AsTracking().SingleAsync(p => p.Id == _pdfDocumentId);
        pdf.ProcessingState = "Ready";
        _db.ChangeTracker.Entries<PdfDocumentEntity>().Single().State.Should().Be(EntityState.Modified);

        _db.Entry(pdf).State = EntityState.Unchanged;

        pdf.ProcessingState.Should().Be("Pending");
    }

    private sealed class FailOnDemandSaveInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public Func<DbContext, bool>? FailWhen { get; set; }

        public Func<Exception>? ExceptionToThrow { get; set; }

        public int Failures { get; private set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context && FailWhen?.Invoke(context) == true)
            {
                Failures++;
                throw ExceptionToThrow?.Invoke() ?? new DbUpdateException("simulated save failure");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private PdfProcessingPipelineService CreatePipeline()
    {
        var languageDetectorMock = new Mock<ILanguageDetector>();
        languageDetectorMock
            .Setup(l => l.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetectionResult("en", true, 0.95));

        return new PdfProcessingPipelineService(
            _db,
            new InMemoryPdfClaimService(_db),
            _pdfTextExtractorMock.Object,
            _tableExtractorMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _blobStorageServiceMock.Object,
            TimeProvider.System,
            NullLogger<PdfProcessingPipelineService>.Instance,
            languageDetectorMock.Object,
            _chunkTranslationServiceMock.Object,
            _indexingPipelineMock.Object,
            vectorStore: _vectorStoreMock.Object,
            roleClassifier: _roleClassifierMock.Object,
            advancedChunking: _advancedChunkingMock.Object);
    }

    private void SeedPdfDocument()
    {
        // PrivateGameId so PdfGameIdResolver returns _gameId directly (no games-table lookup),
        // mirroring PdfProcessingPipelineHeadingAwareTests.SeedPdfDocument.
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfDocumentId,
            PrivateGameId = _gameId,
            FileName = "test.pdf",
            FilePath = "/fake/path/test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Pending",
            UploadedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    private void SetupBlobStorage()
    {
        _blobStorageServiceMock
            .Setup(b => b.RetrieveAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF header
    }

    private void SetupExtractor(int pageCount)
    {
        var pageChunks = Enumerable.Range(1, pageCount)
            .Select(p => new PageTextChunk(
                PageNumber: p,
                Text: $"chunk{p} text",
                CharStartIndex: (p - 1) * 50,
                CharEndIndex: p * 50))
            .ToList();

        var structuredElements = new List<ExtractedElement>
        {
            new("Setup", 1, "Title"),
            new("Place the tiles.", 1, "NarrativeText")
        };

        var result = PagedTextExtractionResult.CreateSuccess(
            pageChunks: pageChunks,
            totalPages: pageCount,
            totalCharacters: pageChunks.Sum(pc => pc.Text.Length),
            ocrTriggered: false,
            structuredElements: structuredElements);

        _pdfTextExtractorMock
            .Setup(e => e.ExtractPagedTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        _tableExtractorMock
            .Setup(t => t.ExtractStructuredContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StructuredContentResult.CreateFailure("Not applicable"));
    }

    /// <summary>
    /// Parent (Level 0) + two children (Level 2), each with distinct body text so the resulting
    /// chunk inputs are unambiguous. Mirrors the minimal hierarchy shape
    /// <c>HeadingAwareChunker.BuildAsync</c> / <c>HierarchicalChunkMapper</c> produce.
    /// </summary>
    private void SetupHierarchicalChunking()
    {
        var parent = HierarchicalChunk.CreateParent(
            "Setup section content",
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "Title", DocumentId = _pdfDocumentId });
        var child1 = HierarchicalChunk.CreateChild(
            "Place the tiles on the board.",
            level: 2,
            new ChunkMetadata { Page = 1, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfDocumentId },
            parent.Id);
        var child2 = HierarchicalChunk.CreateChild(
            "Each player draws three cards.",
            level: 2,
            new ChunkMetadata { Page = 2, Heading = "Setup", ElementType = "NarrativeText", DocumentId = _pdfDocumentId },
            parent.Id);

        _advancedChunkingMock
            .Setup(s => s.ChunkDocumentAsync(
                It.IsAny<ExtractedDocument>(),
                It.IsAny<ChunkingConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk> { parent, child1, child2 });
    }

    private void SetupEmbeddings()
    {
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
                new EmbeddingResult
                {
                    Success = true,
                    Embeddings = texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f }).ToList()
                });
        _embeddingServiceMock.Setup(e => e.GetModelName()).Returns("test-embedding-model");
    }

    /// <summary>
    /// Distinct, all-NON-None role per chunk so a working denormalization produces non-zero
    /// <c>RoleTags</c> on every pgvector row (and a broken one leaves them all 0).
    /// </summary>
    private void SetupRoleClassifierDistinctPerChunk()
    {
        var cycle = new[] { GameBookRole.Setup, GameBookRole.RulesReference, GameBookRole.Lore };
        _roleClassifierMock
            .Setup(c => c.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ChunkInput> inputs, CancellationToken _) =>
                (IReadOnlyList<GameBookRole>)inputs.Select((_, i) => cycle[i % cycle.Length]).ToList());
    }

    /// <summary>
    /// The real <c>IPdfIndexingPipeline</c> creates the <c>vector_documents</c> row the pipeline
    /// re-reads before indexing. The fake reproduces that side effect so <c>IndexInVectorStoreAsync</c>
    /// reaches the pgvector batch build (otherwise it returns early on a missing VectorDocument).
    /// </summary>
    private void SetupIndexingPipelineCreatesVectorDocument()
    {
        // #4609: the row is created by BeginIndexingAsync (in "processing"); IndexAsync, called last, only
        // completes it, so the default mock is enough for it.
        _indexingPipelineMock
            .Setup(p => p.BeginIndexingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid docId, Guid? gid, Guid? sgid, int chunkCount, int totalChars, string lang, CancellationToken ct) =>
            {
                _db.VectorDocuments.Add(new VectorDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    PdfDocumentId = docId,
                    GameId = gid,
                    SharedGameId = sgid,
                    ChunkCount = chunkCount,
                    TotalCharacters = totalChars,
                    IndexingStatus = "processing"
                });
                await _db.SaveChangesAsync(ct);
            });
    }
}
