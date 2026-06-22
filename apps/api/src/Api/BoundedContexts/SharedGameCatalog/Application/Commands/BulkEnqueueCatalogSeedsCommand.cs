using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to enqueue multiple catalog seed drafts in a single batch.
/// Delegates per-item insert (and duplicate detection) to
/// <see cref="EnqueueCatalogSeedCommand"/> via MediatR.
/// Hard cap: 100 BGG ids per batch (enforced by validator).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §4.2 / §6.
/// Issue #1903 — M4.3.
/// </summary>
internal sealed record BulkEnqueueCatalogSeedsCommand(
    IReadOnlyList<int> BggIds,
    Guid CreatedByUserId) : ICommand<BulkEnqueueCatalogSeedsResult>;

/// <summary>
/// Result of a bulk-enqueue operation.
/// </summary>
/// <param name="Total">Total ids submitted in the batch.</param>
/// <param name="Enqueued">Number of newly created draft rows.</param>
/// <param name="Duplicates">Number of ids skipped because a draft already existed.</param>
/// <param name="NewDraftIds">Ids of the newly created drafts (length == Enqueued).</param>
internal sealed record BulkEnqueueCatalogSeedsResult(
    int Total,
    int Enqueued,
    int Duplicates,
    IReadOnlyList<Guid> NewDraftIds);
