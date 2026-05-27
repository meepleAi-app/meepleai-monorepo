using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;

/// <summary>
/// Query for <c>ListUserKbDocsQueryHandler</c> — paginated list of the
/// caller's KB documents across all games (BE-1 #1588).
/// </summary>
/// <param name="UserId">Authenticated user's id (derived from session principal, not query string).</param>
/// <param name="Page">1-based page index; default 1.</param>
/// <param name="PageSize">Items per page; default 20; valid range [1, 100].</param>
/// <param name="SortBy">Sort key. v1 supports only "recent" (ProcessedAt ?? UploadedAt DESC); null means default.</param>
/// <param name="State">Processing-state filter. "ready" (default) excludes in-flight; "all" includes every state.</param>
public sealed record ListUserKbDocsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? State = null) : IQuery<KbDocsListResponse>;

/// <summary>
/// Paginated response shape — mirrors the FE-side <c>PaginatedLibraryResponse</c> envelope.
/// </summary>
public sealed record KbDocsListResponse(
    IReadOnlyList<UserKbDocDto> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>
/// Single KB doc projection for the user-scoped cross-game listing.
/// </summary>
/// <param name="Id">PDF document id.</param>
/// <param name="GameId">Shared game id (null for orphaned / private-game docs).</param>
/// <param name="GameName">Shared game display name (null when <paramref name="GameId"/> is null or not resolved).</param>
/// <param name="FileName">PDF file name (with extension).</param>
/// <param name="ProcessingState">
///   Raw 8-value <c>PdfProcessingState</c> string — one of
///   <c>Pending|Uploading|Extracting|Chunking|Embedding|Indexing|Ready|Failed</c>.
///   FE consumers map to their own labels (decided in the spec panel).
/// </param>
/// <param name="PageCount">Number of pages; null until extraction completes.</param>
/// <param name="ProcessedAt">Timestamp of the most recent processing state transition; null for Pending docs that never started.</param>
/// <param name="UploadedAt">Upload timestamp; always non-null. Used as the sort fallback when <paramref name="ProcessedAt"/> is null.</param>
public sealed record UserKbDocDto(
    Guid Id,
    Guid? GameId,
    string? GameName,
    string FileName,
    string ProcessingState,
    int? PageCount,
    DateTime? ProcessedAt,
    DateTime UploadedAt);
