using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Aggregate root representing a batch of scanned rulebook photos uploaded by a user.
/// Tracks overall processing state, per-page indexing progress, and raises
/// <see cref="PhotoBatchCompletedEvent"/> when all pages have been indexed.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
public sealed class PhotoBatchUpload : AggregateRoot<Guid>
{
    /// <summary>Gets the ID of the user who initiated this upload.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the game this batch is linked to.</summary>
    public Guid GameId { get; private set; }

    /// <summary>Gets the ISO 639-1 source language hint for OCR.</summary>
    public string SourceLanguage { get; private set; } = null!;

    /// <summary>Gets the current processing status of the batch.</summary>
    public PhotoBatchStatus Status { get; private set; }

    /// <summary>Gets the declared total number of pages in this batch.</summary>
    public int TotalPages { get; private set; }

    /// <summary>Gets the number of pages that have been successfully indexed so far.</summary>
    public int IndexedPages { get; private set; }

    /// <summary>Gets the UTC timestamp when this batch was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when this batch reached a terminal state, if applicable.</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Gets whether this batch has been soft-deleted.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Gets the UTC timestamp of soft deletion, if applicable.</summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>EF Core optimistic concurrency token.</summary>
    [Timestamp]
    public byte[] RowVersion { get; private set; } = null!;

    private readonly List<PhotoBatchPage> _pages = [];

    /// <summary>Gets the pages that have been attached to this batch.</summary>
    public IReadOnlyCollection<PhotoBatchPage> Pages => _pages.AsReadOnly();

#pragma warning disable CS8618
    private PhotoBatchUpload() : base() { } // EF Core
#pragma warning restore CS8618

    /// <summary>
    /// Creates a new <see cref="PhotoBatchUpload"/> in <see cref="PhotoBatchStatus.Pending"/> state.
    /// </summary>
    /// <param name="userId">The user initiating the upload.</param>
    /// <param name="gameId">The game this batch is linked to.</param>
    /// <param name="sourceLanguage">ISO 639-1 language code for OCR hint (e.g. "en", "it").</param>
    /// <param name="totalPages">The total number of pages declared by the client. Must be positive.</param>
    /// <exception cref="ArgumentException">
    ///   Thrown when <paramref name="totalPages"/> is not positive, or
    ///   <paramref name="sourceLanguage"/> is null or whitespace.
    /// </exception>
    public static PhotoBatchUpload Create(Guid userId, Guid gameId, string sourceLanguage, int totalPages)
    {
        if (totalPages <= 0)
            throw new ArgumentException("Total pages must be positive", nameof(totalPages));
        if (string.IsNullOrWhiteSpace(sourceLanguage))
            throw new ArgumentException("Source language required", nameof(sourceLanguage));

        return new PhotoBatchUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SourceLanguage = sourceLanguage,
            Status = PhotoBatchStatus.Pending,
            TotalPages = totalPages,
            IndexedPages = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions the batch from <see cref="PhotoBatchStatus.Pending"/> to
    /// <see cref="PhotoBatchStatus.Processing"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the batch is not in Pending state.</exception>
    public void StartProcessing()
    {
        if (Status != PhotoBatchStatus.Pending)
            throw new InvalidOperationException($"Cannot start processing from status {Status}");

        Status = PhotoBatchStatus.Processing;
    }

    /// <summary>
    /// Records that one page has been indexed. When all pages are accounted for,
    /// transitions to <see cref="PhotoBatchStatus.Completed"/> and raises
    /// <see cref="PhotoBatchCompletedEvent"/>.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number that was indexed.</param>
    /// <param name="confidence">OCR confidence score for the page (0.0–1.0).</param>
    /// <param name="warnings">Any OCR warnings produced for this page.</param>
    public void RecordPageIndexed(int pageNumber, double confidence, string[] warnings)
    {
        IndexedPages++;

        if (IndexedPages >= TotalPages)
        {
            Status = PhotoBatchStatus.Completed;
            CompletedAt = DateTime.UtcNow;

            var lowConfidenceCount = _pages.Count(p => p.Confidence < 0.7);
            AddDomainEvent(new PhotoBatchCompletedEvent(Id, UserId, GameId, TotalPages, lowConfidenceCount));
        }
    }

    /// <summary>
    /// Attaches a processed <see cref="PhotoBatchPage"/> to this batch.
    /// </summary>
    /// <param name="page">The page to attach.</param>
    /// <exception cref="InvalidOperationException">Thrown when the page belongs to a different batch.</exception>
    public void AttachPage(PhotoBatchPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.PhotoBatchUploadId != Id)
            throw new InvalidOperationException("Page batch ID mismatch");

        _pages.Add(page);
    }

    /// <summary>
    /// Marks the batch as failed with an optional reason.
    /// </summary>
    /// <param name="reason">Human-readable failure reason.</param>
    public void Fail(string reason)
    {
        Status = PhotoBatchStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-deletes this batch, hiding it from queries while retaining audit history.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}

/// <summary>Processing lifecycle states for a <see cref="PhotoBatchUpload"/>.</summary>
public enum PhotoBatchStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
