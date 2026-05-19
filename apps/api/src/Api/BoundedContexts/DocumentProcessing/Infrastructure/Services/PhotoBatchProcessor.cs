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
/// Thread-safety: IO operations (blob retrieve + preprocess + chunk + KB index) run in
/// parallel up to <c>PhotoBatch:MaxParallelism</c>. Aggregate state mutations
/// (<see cref="PhotoBatchUpload.AttachPage"/> and <see cref="PhotoBatchUpload.RecordPageIndexed"/>)
/// are serialized via <see cref="_aggregateMutex"/> to avoid race conditions on the
/// <see cref="PhotoBatchUpload"/> entity collections.
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

    // Serializes aggregate state mutations across parallel page tasks.
    private readonly SemaphoreSlim _aggregateMutex = new(1, 1);

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

    public void Dispose() => _aggregateMutex.Dispose();

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
        var stream = await _blob.RetrieveAsync(descriptor.BlobKey, BlobCategory.Pdf, gameIdString, ct).ConfigureAwait(false);
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

        // 6. Chunk + index extracted text into KB (outside mutex — parallel-safe IO).
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

        // 7. Serialize aggregate state mutations to avoid concurrent list/counter corruption.
        await _aggregateMutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            batch.AttachPage(page);
            batch.RecordPageIndexed(page.PageNumber, preprocessed.ConfidenceScore, preprocessed.Warnings);
        }
        finally
        {
            _aggregateMutex.Release();
        }

        _logger.LogDebug(
            "Page {PageNumber} of batch {BatchId} indexed (confidence={Confidence:F2}, isBlank={IsBlank})",
            page.PageNumber, batch.Id, preprocessed.ConfidenceScore, preprocessed.IsBlankPage);
    }

    private readonly record struct PageDescriptor(string BlobKey, int Index);
}
