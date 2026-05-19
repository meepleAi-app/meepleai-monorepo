using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

/// <summary>
/// Repository interface for <see cref="PhotoBatchUpload"/> aggregate.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
internal interface IPhotoBatchUploadRepository : IRepository<PhotoBatchUpload, Guid>
{
    /// <summary>
    /// Finds all batches belonging to a user, ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<PhotoBatchUpload>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Loads a batch together with its pages (eager-loaded).
    /// Returns null when not found.
    /// </summary>
    Task<PhotoBatchUpload?> FindByIdWithPagesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the extracted text for a specific page in a batch,
    /// or <c>null</c> if the page does not exist or has no text yet.
    /// Libro Game AI Assistant MVP Phase 3 — G4: GetParagraphQuery.
    /// </summary>
    Task<string?> GetPageTextAsync(Guid uploadId, int pageNumber, CancellationToken ct = default);

    /// <summary>
    /// Returns the extracted text of the first page in a batch whose
    /// <c>paragraph_numbers</c> array contains <paramref name="paragraphNumber"/>,
    /// or <c>null</c> when no page matches.
    /// Issue #747: paragraph-number lookup distinct from physical page index.
    /// </summary>
    /// <remarks>
    /// Several physical pages can carry the same narrative paragraph number when the
    /// pipeline cannot disambiguate; the first page (ordered by <c>page_number</c>) is
    /// returned to keep behavior deterministic. Callers needing every match should issue
    /// a dedicated query.
    /// </remarks>
    Task<string?> GetPageTextByParagraphNumberAsync(
        Guid uploadId,
        int paragraphNumber,
        CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> when the batch exists AND belongs to the specified user.
    /// Used for ownership-based authorization before fetching page text.
    /// Libro Game AI Assistant MVP Phase 3 — G4: GetParagraphQuery.
    /// </summary>
    Task<bool> BelongsToUserAsync(Guid uploadId, Guid userId, CancellationToken ct = default);
}
