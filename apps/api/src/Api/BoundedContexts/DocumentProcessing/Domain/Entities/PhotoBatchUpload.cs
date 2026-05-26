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

    /// <summary>Gets the human-readable reason for failure, if the batch failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>EF Core optimistic concurrency token.
    /// Configured exclusively via <c>PhotoBatchUploadEntityConfiguration.IsRowVersion()</c>;
    /// the previous <c>[Timestamp]</c> data annotation combined with
    /// <c>.IsRowVersion()</c> produced a double-mapping that, under the Npgsql
    /// provider, made EF send an explicit NULL on INSERT and violate the NOT-NULL
    /// constraint on <c>row_version bytea</c>. The pattern aligned here mirrors
    /// <see cref="Api.Infrastructure.Entities.UserLibrary.UserLibraryEntryEntity"/>
    /// which works correctly without the annotation. Migration
    /// <c>20260524190307_FixPhotoBatchUploadRowVersionNullable</c> makes the
    /// underlying column nullable so the Npgsql provider can omit it from the
    /// INSERT statement and rely on optimistic concurrency on UPDATE.
    /// Setter omitted: EF Core populates the value via reflection.
    /// </summary>
    public byte[]? RowVersion { get; }

    private readonly List<PhotoBatchPage> _pages = [];

    /// <summary>
    /// Accumulated count of pages whose OCR confidence fell below 0.7.
    /// Updated in <see cref="RecordPageIndexed"/> from the <c>confidence</c> parameter
    /// so that the count is correct even when <c>_pages</c> is not eagerly loaded.
    /// Persisted to <c>low_confidence_page_count</c> so the value survives an API
    /// restart mid-batch (Sprint 1 handler uses <c>GetByIdAsync</c> without <c>.Include</c>).
    /// </summary>
    private int _lowConfidencePageCount;

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
        if (Status != PhotoBatchStatus.Processing)
            throw new InvalidOperationException($"Cannot record indexed page from status {Status}");
        if (IndexedPages >= TotalPages)
            throw new InvalidOperationException("All pages already indexed");

        IndexedPages++;
        if (confidence < 0.7)
            _lowConfidencePageCount++;

        if (IndexedPages >= TotalPages)
        {
            Status = PhotoBatchStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            AddDomainEvent(new PhotoBatchCompletedEvent(Id, UserId, GameId, TotalPages, _lowConfidencePageCount));
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
    /// Marks the batch as failed with a human-readable reason.
    /// </summary>
    /// <param name="reason">Human-readable failure reason.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the batch is already in a terminal state (<see cref="PhotoBatchStatus.Completed"/>
    /// or <see cref="PhotoBatchStatus.Failed"/>), protecting the audit invariant.
    /// </exception>
    public void Fail(string reason)
    {
        if (Status is PhotoBatchStatus.Completed or PhotoBatchStatus.Failed)
            throw new InvalidOperationException($"Cannot fail batch from terminal status {Status}");

        Status = PhotoBatchStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        FailureReason = reason;
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
