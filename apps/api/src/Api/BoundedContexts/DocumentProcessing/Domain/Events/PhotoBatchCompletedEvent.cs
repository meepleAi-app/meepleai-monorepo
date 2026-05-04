using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Events;

/// <summary>
/// Domain event raised when all pages in a photo batch have been successfully indexed.
/// Triggers downstream notification and quality-review workflows.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
public sealed class PhotoBatchCompletedEvent : DomainEventBase
{
    /// <summary>Gets the photo batch identifier.</summary>
    public Guid BatchId { get; }

    /// <summary>Gets the user who initiated the upload.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the game this batch is associated with.</summary>
    public Guid GameId { get; }

    /// <summary>Gets the total number of pages processed.</summary>
    public int TotalPages { get; }

    /// <summary>Gets the number of pages whose OCR confidence was below 0.7.</summary>
    public int LowConfidencePages { get; }

    /// <summary>
    /// Initializes a new <see cref="PhotoBatchCompletedEvent"/>.
    /// </summary>
    public PhotoBatchCompletedEvent(
        Guid batchId,
        Guid userId,
        Guid gameId,
        int totalPages,
        int lowConfidencePages)
    {
        BatchId = batchId;
        UserId = userId;
        GameId = gameId;
        TotalPages = totalPages;
        LowConfidencePages = lowConfidencePages;
    }
}
