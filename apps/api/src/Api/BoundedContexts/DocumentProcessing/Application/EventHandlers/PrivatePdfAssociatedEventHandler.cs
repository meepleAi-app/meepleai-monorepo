using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Qdrant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client.Grpc;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Handles PrivatePdfAssociatedEvent to trigger PDF processing pipeline.
/// Issue #3653 - Private PDF Upload Endpoint Full Integration.
///
/// Workflow:
/// 1. Load PDF document and verify ownership
/// 2. Extract text from PDF (uses existing extraction if available)
/// 3. Chunk text into searchable segments
/// 4. Generate embeddings for each chunk
/// 5. Index to private_rules Qdrant collection with user isolation
/// 6. Publish progress updates via SSE stream
///
/// The private_rules collection is separate from the public meepleai_documents
/// collection to ensure user data isolation. Each vector includes user_id and
/// library_entry_id in the payload for filtering.
/// </summary>
internal sealed class PrivatePdfAssociatedEventHandler : INotificationHandler<PrivatePdfAssociatedEvent>
{
    private const string PrivateRulesCollection = "private_rules";

    private readonly MeepleAiDbContext _db;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantCollectionManager _collectionManager;
    private readonly IQdrantVectorIndexer _vectorIndexer;
    private readonly IPrivatePdfProgressStreamService _progressStream;
    private readonly IndexingSettings _indexingSettings;
    private readonly ILogger<PrivatePdfAssociatedEventHandler> _logger;

    public PrivatePdfAssociatedEventHandler(
        MeepleAiDbContext db,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IQdrantCollectionManager collectionManager,
        IQdrantVectorIndexer vectorIndexer,
        IPrivatePdfProgressStreamService progressStream,
        IOptions<IndexingSettings> indexingSettings,
        ILogger<PrivatePdfAssociatedEventHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
        _vectorIndexer = vectorIndexer ?? throw new ArgumentNullException(nameof(vectorIndexer));
        _progressStream = progressStream ?? throw new ArgumentNullException(nameof(progressStream));
        _indexingSettings = indexingSettings?.Value ?? throw new ArgumentNullException(nameof(indexingSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PrivatePdfAssociatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing PrivatePdfAssociatedEvent: PdfId={PdfId}, UserId={UserId}, GameId={GameId}, EntryId={EntryId}",
            notification.PdfDocumentId, notification.UserId, notification.GameId, notification.LibraryEntryId);

        try
        {
            // 1. Publish upload progress (already uploaded, but start the progress stream)
            await PublishProgressAsync(notification, ProcessingProgressJson.ForStep(
                ProcessingStep.Uploading, 20, "File uploaded, starting processing..."), cancellationToken).ConfigureAwait(false);

            // 2. Load and validate PDF document from database
            var pdf = await _db.PdfDocuments
                .FirstOrDefaultAsync(p => p.Id == notification.PdfDocumentId, cancellationToken)
                .ConfigureAwait(false);

            if (pdf is null)
            {
                _logger.LogError("PDF document {PdfId} not found for event processing", notification.PdfDocumentId);
                await PublishProgressAsync(notification, ProcessingProgressJson.Failed("PDF document not found"), cancellationToken).ConfigureAwait(false);
                return;
            }

            // Transition to Extracting state (persist to DB for polling endpoint)
            await UpdateProcessingStateAsync(pdf, "Extracting", ProcessingStep.Extracting, 20, cancellationToken).ConfigureAwait(false);

            // 3. Extract text (check if already extracted)
            string extractedText;
            if (!string.IsNullOrWhiteSpace(pdf.ExtractedText))
            {
                _logger.LogInformation("Using existing extracted text for PDF {PdfId}", notification.PdfDocumentId);
                extractedText = pdf.ExtractedText;
                var pageCount = pdf.PageCount ?? 1;
                await PublishProgressAsync(notification, ProcessingProgressJson.ForExtracting(
                    pageCount, pageCount), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "PDF {PdfId} has no extracted text, processing will be skipped. Text extraction should happen separately.",
                    notification.PdfDocumentId);
                await UpdateProcessingStateAsync(pdf, "Failed", ProcessingStep.Failed, 0, cancellationToken).ConfigureAwait(false);
                await PublishProgressAsync(notification, ProcessingProgressJson.Failed(
                    "PDF text extraction required. Please wait for extraction to complete."), cancellationToken).ConfigureAwait(false);
                return;
            }

            // 4. Chunk text — transition to Chunking state
            await UpdateProcessingStateAsync(pdf, "Chunking", ProcessingStep.Chunking, 45, cancellationToken).ConfigureAwait(false);
            await PublishProgressAsync(notification, ProcessingProgressJson.ForStep(
                ProcessingStep.Chunking, 45, "Creating text chunks..."), cancellationToken).ConfigureAwait(false);

            var textChunks = _chunkingService.ChunkText(extractedText);
            if (textChunks.Count == 0)
            {
                _logger.LogWarning("No chunks created for PDF {PdfId}", notification.PdfDocumentId);
                await UpdateProcessingStateAsync(pdf, "Failed", ProcessingStep.Failed, 0, cancellationToken).ConfigureAwait(false);
                await PublishProgressAsync(notification, ProcessingProgressJson.Failed("No text chunks could be created"), cancellationToken).ConfigureAwait(false);
                return;
            }

            _logger.LogInformation("Created {ChunkCount} chunks for PDF {PdfId}", textChunks.Count, notification.PdfDocumentId);
            await PublishProgressAsync(notification, ProcessingProgressJson.ForChunking(textChunks.Count), cancellationToken).ConfigureAwait(false);

            // 5. Generate embeddings in batches — transition to Embedding state
            await UpdateProcessingStateAsync(pdf, "Embedding", ProcessingStep.Embedding, 60, cancellationToken).ConfigureAwait(false);
            await PublishProgressAsync(notification, ProcessingProgressJson.ForStep(
                ProcessingStep.Embedding, 60, "Generating AI embeddings..."), cancellationToken).ConfigureAwait(false);

            var documentChunks = await GenerateEmbeddingsAsync(
                pdf, notification.PdfDocumentId, textChunks, notification, cancellationToken).ConfigureAwait(false);

            if (documentChunks is null || documentChunks.Count == 0)
            {
                return; // Error already published and DB state updated
            }

            _logger.LogInformation("Generated {EmbeddingCount} embeddings for PDF {PdfId}",
                documentChunks.Count, notification.PdfDocumentId);
            await PublishProgressAsync(notification, ProcessingProgressJson.ForEmbedding(documentChunks.Count), cancellationToken).ConfigureAwait(false);

            // 6. Ensure private_rules collection exists — transition to Indexing state
            await UpdateProcessingStateAsync(pdf, "Indexing", ProcessingStep.Indexing, 85, cancellationToken).ConfigureAwait(false);
            await PublishProgressAsync(notification, ProcessingProgressJson.ForStep(
                ProcessingStep.Indexing, 85, "Preparing vector storage..."), cancellationToken).ConfigureAwait(false);

            var vectorSize = (uint)_embeddingService.GetEmbeddingDimensions();
            await _collectionManager.EnsureCollectionExistsAsync(PrivateRulesCollection, vectorSize, cancellationToken).ConfigureAwait(false);

            // 7. Index to private_rules collection with user isolation payload
            var basePayload = new Dictionary<string, Value>(StringComparer.Ordinal)
            {
                ["user_id"] = notification.UserId.ToString(),
                ["library_entry_id"] = notification.LibraryEntryId.ToString(),
                ["game_id"] = notification.GameId.ToString(),
                ["pdf_id"] = notification.PdfDocumentId.ToString(),
                ["is_private"] = true
            };

            var points = _vectorIndexer.BuildPoints(documentChunks, basePayload);
            await _vectorIndexer.UpsertPointsAsync(PrivateRulesCollection, points.AsReadOnly(), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully indexed {ChunkCount} chunks to private_rules for PDF {PdfId} (User: {UserId}, Entry: {EntryId})",
                documentChunks.Count, notification.PdfDocumentId, notification.UserId, notification.LibraryEntryId);

            await PublishProgressAsync(notification, ProcessingProgressJson.ForIndexing(documentChunks.Count), cancellationToken).ConfigureAwait(false);

            // 8. Update PDF processing status to final state and save
            pdf.ProcessingState = "Ready";
            pdf.ProcessingStatus = "indexed";
            pdf.ProcessedAt = DateTime.UtcNow;
            UpdateProgressJson(pdf, ProcessingStep.Completed, 100);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await PublishProgressAsync(notification, ProcessingProgressJson.Completed(), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Private PDF processing completed: PdfId={PdfId}, Chunks={ChunkCount}, Collection={Collection}",
                notification.PdfDocumentId, documentChunks.Count, PrivateRulesCollection);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // EVENT HANDLER BOUNDARY: Catch all exceptions to prevent event handler failures from crashing the application.
        // Rationale: Domain event handlers should be resilient and log errors without propagating exceptions.
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing PrivatePdfAssociatedEvent for PdfId={PdfId}: {Error}",
                notification.PdfDocumentId, ex.Message);

            // Persist failed state to DB so polling endpoint reflects the error
            try
            {
                var failedPdf = await _db.PdfDocuments
                    .FirstOrDefaultAsync(p => p.Id == notification.PdfDocumentId, cancellationToken)
                    .ConfigureAwait(false);

                if (failedPdf is not null)
                {
                    failedPdf.ProcessingState = "Failed";
                    failedPdf.ProcessingStatus = "failed";
                    failedPdf.ProcessingError = ex.Message;
                    UpdateProgressJson(failedPdf, ProcessingStep.Failed, 0);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
#pragma warning disable CA1031
            catch (Exception dbEx)
            {
                _logger.LogWarning(dbEx, "Failed to persist error state for PDF {PdfId}", notification.PdfDocumentId);
            }
#pragma warning restore CA1031

            await PublishProgressAsync(notification, ProcessingProgressJson.Failed($"Processing failed: {ex.Message}"), cancellationToken).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Generates embeddings for text chunks in batches.
    /// </summary>
    private async Task<List<DocumentChunk>?> GenerateEmbeddingsAsync(
        PdfDocumentEntity pdf,
        Guid pdfId,
        List<TextChunk> textChunks,
        PrivatePdfAssociatedEvent notification,
        CancellationToken cancellationToken)
    {
        var embeddingBatchSize = _indexingSettings.EmbeddingBatchSize;
        var documentChunks = new List<DocumentChunk>(textChunks.Count);

        _logger.LogInformation("Generating embeddings for {ChunkCount} chunks in batches of {BatchSize}",
            textChunks.Count, embeddingBatchSize);

        for (int i = 0; i < textChunks.Count; i += embeddingBatchSize)
        {
            var batchSize = Math.Min(embeddingBatchSize, textChunks.Count - i);
            var batchNumber = (i / embeddingBatchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)textChunks.Count / embeddingBatchSize);

            _logger.LogDebug("Processing embedding batch {BatchNumber}/{TotalBatches} ({BatchSize} chunks)",
                batchNumber, totalBatches, batchSize);

            var texts = textChunks.Skip(i).Take(batchSize).Select(c => c.Text).ToList();
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken).ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate embeddings for PDF {PdfId}: {Error}",
                    pdfId, embeddingResult.ErrorMessage);
                await UpdateProcessingStateAsync(pdf, "Failed", ProcessingStep.Failed, 0, cancellationToken).ConfigureAwait(false);
                await PublishProgressAsync(notification, ProcessingProgressJson.Failed(
                    $"Embedding generation failed: {embeddingResult.ErrorMessage}"), cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (embeddingResult.Embeddings.Count != batchSize)
            {
                _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}",
                    batchSize, embeddingResult.Embeddings.Count);
                await UpdateProcessingStateAsync(pdf, "Failed", ProcessingStep.Failed, 0, cancellationToken).ConfigureAwait(false);
                await PublishProgressAsync(notification, ProcessingProgressJson.Failed(
                    "Embedding count mismatch"), cancellationToken).ConfigureAwait(false);
                return null;
            }

            // Prepare document chunks with embeddings
            var batchChunks = textChunks.Skip(i).Take(batchSize)
                .Select((chunk, index) => new DocumentChunk
                {
                    Text = chunk.Text,
                    Embedding = embeddingResult.Embeddings[index],
                    Page = chunk.Page,
                    CharStart = chunk.CharStart,
                    CharEnd = chunk.CharEnd
                })
                .ToList();

            documentChunks.AddRange(batchChunks);

            // Publish intermediate progress (SSE + DB)
            var progress = 60 + (int)(20 * ((double)documentChunks.Count / textChunks.Count));
            UpdateProgressJson(pdf, ProcessingStep.Embedding, progress);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await PublishProgressAsync(notification, ProcessingProgressJson.ForStep(
                ProcessingStep.Embedding, progress,
                $"Generated {documentChunks.Count}/{textChunks.Count} embeddings..."), cancellationToken).ConfigureAwait(false);
        }

        return documentChunks;
    }

    /// <summary>
    /// Updates processing state and progress in the database so the polling endpoint reflects current progress.
    /// </summary>
    private async Task UpdateProcessingStateAsync(
        PdfDocumentEntity pdf,
        string state,
        ProcessingStep step,
        int percent,
        CancellationToken cancellationToken)
    {
        pdf.ProcessingState = state;
        pdf.ProcessingStatus = string.Equals(state, "Failed", StringComparison.Ordinal) ? "failed" : "processing";
        UpdateProgressJson(pdf, step, percent);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the ProcessingProgressJson field on the entity (without saving).
    /// </summary>
    private static void UpdateProgressJson(PdfDocumentEntity pdf, ProcessingStep step, int percent)
    {
        pdf.ProcessingProgress = new ProcessingProgress
        {
            CurrentStep = step,
            PercentComplete = percent,
            PagesProcessed = pdf.PageCount ?? 0,
            TotalPages = pdf.PageCount ?? 0,
            StartedAt = pdf.UploadedAt,
        };
    }

    /// <summary>
    /// Publishes progress to the SSE stream if there are active subscribers.
    /// </summary>
    private async Task PublishProgressAsync(
        PrivatePdfAssociatedEvent notification,
        ProcessingProgressJson progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await _progressStream.PublishProgressAsync(
                notification.UserId,
                notification.LibraryEntryId,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // INFRASTRUCTURE BOUNDARY: Progress publishing failure should not fail the entire operation.
        // Rationale: SSE streaming is best-effort - if there are no subscribers or the stream fails, we continue processing.
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish progress for Entry {EntryId}: {Error}",
                notification.LibraryEntryId, ex.Message);
            // Don't fail the entire operation if progress publishing fails
        }
#pragma warning restore CA1031
    }
}
