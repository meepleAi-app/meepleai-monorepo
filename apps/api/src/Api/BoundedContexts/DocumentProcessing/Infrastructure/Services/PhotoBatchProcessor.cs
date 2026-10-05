using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Parallel batch processor for photo uploads in the Libro Game AI Assistant pipeline.
/// Retrieves page blobs, preprocesses each via OCR, chunks the extracted text, and
/// indexes chunks into the KB vector store before recording page state on the aggregate.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.6 / Phase 2 — Task 2.3.
/// </summary>
/// <remarks>
/// Thread-safety: only the EF-free part of a page (blob retrieve + OCR preprocess + paragraph
/// extraction) runs in parallel, up to <c>PhotoBatch:MaxParallelism</c>. KB indexing and the
/// aggregate state mutations (<see cref="PhotoBatchUpload.AttachPage"/> and
/// <see cref="PhotoBatchUpload.RecordPageIndexed"/>) are serialized together via
/// <see cref="_persistenceMutex"/> — see the comment in <c>ProcessSinglePageAsync</c> for why the
/// two cannot be separated.
///
/// KB indexing failure is treated as a non-fatal degradation: if <see cref="IKnowledgeBaseIndexer"/>
/// throws, the error is logged and page state is still recorded normally (batch completes).
/// Vector store persistence is stubbed in <see cref="KnowledgeBaseIndexer"/> pending Gap G3.
/// </remarks>
internal sealed class PhotoBatchProcessor : IPhotoBatchProcessor, IDisposable
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;
    private readonly IPhotoPreprocessor _preprocessor;
    private readonly IDocumentChunker _chunker;
    private readonly IKnowledgeBaseIndexer _kbIndexer;
    private readonly IParagraphNumberExtractor _paragraphExtractor;
    private readonly IUnitOfWork _uow;
    private readonly int _maxParallelism;
    private readonly ILogger<PhotoBatchProcessor> _logger;

    // Serializes KB indexing + aggregate state mutations across parallel page tasks: entrambi
    // passano dal MeepleAiDbContext scoped del batch, che non è thread-safe (#4059).
    private readonly SemaphoreSlim _persistenceMutex = new(1, 1);

    public PhotoBatchProcessor(
        IPhotoBatchUploadRepository repo,
        IBlobStorageService blob,
        IPhotoPreprocessor preprocessor,
        IDocumentChunker chunker,
        IKnowledgeBaseIndexer kbIndexer,
        IParagraphNumberExtractor paragraphExtractor,
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<PhotoBatchProcessor> logger)
    {
        _repo = repo;
        _blob = blob;
        _preprocessor = preprocessor;
        _chunker = chunker;
        _kbIndexer = kbIndexer;
        _paragraphExtractor = paragraphExtractor;
        _uow = uow;
        _maxParallelism = config.GetValue<int?>("PhotoBatch:MaxParallelism") ?? 4;
        _logger = logger;
    }

    public void Dispose() => _persistenceMutex.Dispose();

    /// <inheritdoc/>
    public async Task ProcessAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _repo.FindByIdWithPagesAsync(batchId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Batch {batchId} not found");

        // Transition to Processing if still Pending (idempotent: skip if already Processing).
        if (batch.Status == PhotoBatchStatus.Pending)
        {
            batch.StartProcessing();
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var gameIdString = batch.GameId.ToString();

        // Build page descriptors: blobKey and 0-based index → 1-based page number.
        var pageDescriptors = Enumerable.Range(0, batch.TotalPages)
            .Select(i => new PageDescriptor(
                BlobKey: $"photo-batch-{batchId}-page-{i:D3}.jpg",
                Index: i))
            .ToList();

        var ioSemaphore = new SemaphoreSlim(_maxParallelism);
        try
        {
            var tasks = pageDescriptors.Select(async descriptor =>
            {
                await ioSemaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await ProcessSinglePageAsync(batch, descriptor, gameIdString, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw; // propagate cancellation
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Unhandled error processing page {PageIndex} (blobKey={BlobKey}) of batch {BatchId}",
                        descriptor.Index, descriptor.BlobKey, batchId);
                }
                finally
                {
                    ioSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            ioSemaphore.Dispose();
        }

        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Batch {BatchId} processing complete: {Indexed}/{Total} pages indexed",
            batchId, batch.IndexedPages, batch.TotalPages);
    }

    private async Task ProcessSinglePageAsync(
        PhotoBatchUpload batch,
        PageDescriptor descriptor,
        string gameIdString,
        CancellationToken ct)
    {
        // 1. Retrieve blob from storage.
        var stream = await _blob.RetrieveAsync(descriptor.BlobKey, BlobCategory.PhotoBatch, gameIdString, ct).ConfigureAwait(false);
        if (stream is null)
        {
            _logger.LogWarning(
                "Blob {BlobKey} not found for batch {BatchId} — skipping page {PageIndex}",
                descriptor.BlobKey, batch.Id, descriptor.Index);
            return;
        }

        // 2. Read blob bytes (using-dispose per IBlobStorageService contract).
        byte[] imageData;
        using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            imageData = ms.ToArray();
        }

        // 3. Preprocess image via OCR service (IO-parallel, outside mutex).
        var preprocessed = await _preprocessor.PreprocessAsync(imageData, ct).ConfigureAwait(false);

        // 4. Extract narrative paragraph numbers from OCR text (issue #747 PR-C).
        //    Blank pages return [] without invoking the regex pipeline. Failure here
        //    is silent (logged warning) so a noisy OCR page cannot abort the batch —
        //    the page is still stored without paragraph metadata, and queries fall
        //    back to either the page-number lookup or semantic search.
        var paragraphNumbers = Array.Empty<int>();
        if (!preprocessed.IsBlankPage && !string.IsNullOrWhiteSpace(preprocessed.ExtractedText))
        {
            try
            {
                paragraphNumbers = _paragraphExtractor.Extract(preprocessed.ExtractedText);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "[PhotoBatchProcessor] Paragraph extraction failed for page {PageNumber} of batch {BatchId} — storing page without paragraph metadata",
                    descriptor.Index + 1, batch.Id);
            }
        }

        // 5. Create page entity.
        var page = PhotoBatchPage.Create(
            batchId: batch.Id,
            pageNumber: descriptor.Index + 1,
            blobKey: descriptor.BlobKey,
            confidence: preprocessed.ConfidenceScore,
            orientation: preprocessed.DetectedOrientation,
            isBlank: preprocessed.IsBlankPage,
            warnings: preprocessed.Warnings,
            extractedText: preprocessed.ExtractedText,
            paragraphNumbers: paragraphNumbers);

        // 🔴 Passi 6 e 7 sotto UN SOLO mutex: l'indicizzazione KB tocca EF, e il commento che
        // stava sul passo 6 («outside mutex — parallel-safe IO») era falso.
        //
        // `_kbIndexer.IndexBatchAsync` arriva a `KnowledgeBaseIngestService.IngestChunksAsync`, che
        // fa tre operazioni EF — `GetByGameAndSourceAsync`, `AddBatchAsync`, `SaveChangesAsync` —
        // e tutte passano dal `MeepleAiDbContext` del batch: `VectorDocumentRepository` e
        // `EmbeddingRepository` derivano da `RepositoryBase`, `UnitOfWork` tiene lo stesso contesto
        // iniettato. `EnqueuePhotoBatchProcessingCommandHandler` crea UNO scope per batch, non per
        // pagina, quindi fino a `PhotoBatch:MaxParallelism` pagine avviate insieme condividevano
        // quell'unica istanza — e un DbContext non è thread-safe (#4059).
        //
        // Sintomo: nessun 500 possibile, questo gira in background. L'eccezione veniva ingoiata dal
        // catch qui sotto e di nuovo in `ProcessAsync`, mentre `RecordPageIndexed` registrava la
        // pagina come indicizzata comunque: `GET /api/v1/photo-batches/{id}` riportava `IndexedPages`
        // completo mentre la KB non aveva gli embedding di quelle pagine. È la forma di #3843 —
        // un'eccezione ingoiata che si presenta come assenza. A differenza del caso wizard-preview
        // la collisione era probabilistica, non certa: la finestra EF di ogni pagina è breve rispetto
        // a blob + OCR + embedding che la precedono, quindi le finestre si sovrapponevano per caso.
        //
        // Secondo difetto chiuso dallo stesso mutex: `IngestChunksAsync` chiama `SaveChangesAsync`
        // sul contesto condiviso, e prima stava FUORI dal lock che proteggeva le mutazioni
        // dell'aggregato — un salvataggio di una pagina poteva flushare `batch` a metà
        // dell'`AttachPage`/`RecordPageIndexed` di un'altra. Perché funzioni, salvataggio e mutazioni
        // devono stare sotto lo stesso lock: è il motivo per cui i due passi sono ora uno.
        //
        // Il parallelismo utile resta: blob retrieve, OCR e estrazione paragrafi — il tratto
        // dominante e privo di EF — girano ancora fino a `_maxParallelism` pagine insieme. Si perde
        // la sovrapposizione degli embedding, perché l'ACL `IKnowledgeBaseIndexer` fa embedding e
        // persistenza in una chiamata sola; separarli è un cambio di contratto cross-BC, non di
        // questo file. Non c'è `IDbContextFactory` registrato nel progetto, quindi un contesto per
        // pagina non è un'opzione disponibile.
        await _persistenceMutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 6. Chunk + index extracted text into KB.
            //    Skip blank pages and pages with no extracted text.
            //    KB indexing failure is non-fatal: log and continue so page state is still recorded.
            if (!preprocessed.IsBlankPage && !string.IsNullOrWhiteSpace(preprocessed.ExtractedText))
            {
                try
                {
                    var chunks = _chunker.ChunkPage(
                        batch.Id, page.Id, page.PageNumber,
                        preprocessed.ExtractedText, batch.SourceLanguage, (float)preprocessed.ConfidenceScore);

                    if (chunks.Count > 0)
                    {
                        var indexed = await _kbIndexer.IndexBatchAsync(
                            batch.Id, batch.GameId, chunks, progress: null, ct).ConfigureAwait(false);

                        _logger.LogDebug(
                            "[PhotoBatchProcessor] Page {PageNumber} of batch {BatchId}: {ChunkCount} chunks, {IndexedCount} indexed",
                            page.PageNumber, batch.Id, chunks.Count, indexed);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // KB indexing failure must NOT abort the batch — page state still recorded below.
                    _logger.LogError(ex,
                        "[PhotoBatchProcessor] KB indexing failed for page {PageNumber} of batch {BatchId} — continuing",
                        page.PageNumber, batch.Id);
                }
            }

            // 7. Aggregate state mutations: same lock as the KB write above, so no SaveChangesAsync
            //    can flush the aggregate half-mutated.
            batch.AttachPage(page);
            batch.RecordPageIndexed(page.PageNumber, preprocessed.ConfidenceScore, preprocessed.Warnings);
        }
        finally
        {
            _persistenceMutex.Release();
        }

        _logger.LogDebug(
            "Page {PageNumber} of batch {BatchId} indexed (confidence={Confidence:F2}, isBlank={IsBlank})",
            page.PageNumber, batch.Id, preprocessed.ConfidenceScore, preprocessed.IsBlankPage);
    }

    private readonly record struct PageDescriptor(string BlobKey, int Index);
}
