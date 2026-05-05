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
/// Retrieves page blobs, preprocesses each via OCR, and records indexed pages on the aggregate.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.6.
/// </summary>
/// <remarks>
/// Thread-safety: IO operations (blob retrieve + preprocess) run in parallel up to
/// <c>PhotoBatch:MaxParallelism</c>. Aggregate state mutations
/// (<see cref="PhotoBatchUpload.AttachPage"/> and <see cref="PhotoBatchUpload.RecordPageIndexed"/>)
/// are serialized via <see cref="_aggregateMutex"/> to avoid race conditions on the
/// <see cref="PhotoBatchUpload"/> entity collections.
///
/// KB indexing (IDocumentChunker, IEmbeddingService, IKnowledgeBaseIndexer) is intentionally
/// excluded from this class — deferred to Phase 2 Task 2.3 once those KnowledgeBase BC
/// services are implemented.
/// </remarks>
internal sealed class PhotoBatchProcessor : IPhotoBatchProcessor, IDisposable
{
    private readonly IPhotoBatchUploadRepository _repo;
    private readonly IBlobStorageService _blob;
    private readonly IPhotoPreprocessor _preprocessor;
    private readonly IUnitOfWork _uow;
    private readonly int _maxParallelism;
    private readonly ILogger<PhotoBatchProcessor> _logger;

    // Serializes aggregate state mutations across parallel page tasks.
    private readonly SemaphoreSlim _aggregateMutex = new(1, 1);

    public PhotoBatchProcessor(
        IPhotoBatchUploadRepository repo,
        IBlobStorageService blob,
        IPhotoPreprocessor preprocessor,
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<PhotoBatchProcessor> logger)
    {
        _repo = repo;
        _blob = blob;
        _preprocessor = preprocessor;
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
        var stream = await _blob.RetrieveAsync(descriptor.BlobKey, gameIdString, ct).ConfigureAwait(false);
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

        // 4. Create page entity.
        var page = PhotoBatchPage.Create(
            batchId: batch.Id,
            pageNumber: descriptor.Index + 1,
            blobKey: descriptor.BlobKey,
            confidence: preprocessed.ConfidenceScore,
            orientation: preprocessed.DetectedOrientation,
            isBlank: preprocessed.IsBlankPage,
            warnings: preprocessed.Warnings,
            extractedText: preprocessed.ExtractedText);

        // 5. Serialize aggregate state mutations to avoid concurrent list/counter corruption.
        await _aggregateMutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            batch.AttachPage(page);

#pragma warning disable S1135, MA0026
            // TODO Phase 2 Task 2.3: chunk + embed + KB index here when KnowledgeBase BC
            // services are ready (IDocumentChunker, IEmbeddingService, IKnowledgeBaseIndexer
            // are not yet implemented in Phase 1). The extracted text is already on `page`.
#pragma warning restore S1135, MA0026

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
