namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Contract for processing a photo batch through the OCR pipeline.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.5 (stub).
/// Implementation provided in Task 1.6.
/// </summary>
internal interface IPhotoBatchProcessor
{
    /// <summary>
    /// Processes all pages in the specified batch through OCR and indexing.
    /// </summary>
    /// <param name="batchId">ID of the <see cref="Domain.Entities.PhotoBatchUpload"/> to process.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessAsync(Guid batchId, CancellationToken ct = default);
}
