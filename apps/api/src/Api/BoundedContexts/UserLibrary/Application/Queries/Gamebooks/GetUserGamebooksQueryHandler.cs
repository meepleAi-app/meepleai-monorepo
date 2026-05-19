using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;

/// <summary>
/// Handler for <see cref="GetUserGamebooksQuery"/>.
///
/// Issue #1288 refactor — replaces the original PrivateGame-only lookup
/// (Issue #869 MVP) with a cross-aggregate view that surfaces SharedGame
/// library entries as well, provided they have either:
///   - an active GamebookCampaignSession, OR
///   - an associated PrivateGameId (private rulebook upload)
///
/// Issue #1292 (AC-6): wraps the repository call with
/// <see cref="IHybridCacheService.GetOrCreateAsync"/> for sub-100ms P95
/// on cache hit. Cache key namespaced under <c>userlibrary:gamebooks:view:{userId}</c>,
/// tagged with <c>user:{userId}</c> for selective invalidation via
/// <c>GamebookCacheInvalidationHandler</c>. TTL 5 minutes (default for read-side
/// indexes per HybridCache config).
///
/// Emits Prometheus counter <c>gamebook_index_empty_responses_total</c> when the
/// resulting list is empty, with label <c>reason</c> distinguishing legit empty
/// libraries from filter-too-strict cases (UX signal) and backend errors.
/// </summary>
internal sealed class GetUserGamebooksQueryHandler
    : IQueryHandler<GetUserGamebooksQuery, IReadOnlyList<GamebookCardDataDto>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IUserGamebookViewRepository _viewRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly IHybridCacheService _cache;

    public GetUserGamebooksQueryHandler(
        IUserGamebookViewRepository viewRepository,
        IUserLibraryRepository userLibraryRepository,
        IHybridCacheService cache)
    {
        _viewRepository = viewRepository
            ?? throw new ArgumentNullException(nameof(viewRepository));
        _userLibraryRepository = userLibraryRepository
            ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<IReadOnlyList<GamebookCardDataDto>> Handle(
        GetUserGamebooksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<UserGamebookViewItem> entries;
        try
        {
            entries = await _cache.GetOrCreateAsync(
                cacheKey: BuildCacheKey(query.UserId),
                factory: async ct =>
                {
                    var fromDb = await _viewRepository
                        .GetGamebookEntriesAsync(query.UserId, ct)
                        .ConfigureAwait(false);
                    return fromDb.ToList();
                },
                tags: new[] { BuildUserTag(query.UserId) },
                expiration: CacheTtl,
                ct: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // AC-6.3 reason="backend_error" — operational alert.
            MeepleAiMetrics.GamebookIndexEmptyResponsesTotal.Add(1,
                new KeyValuePair<string, object?>("reason", GamebookEmptyReasons.BackendError));
            throw;
        }

        var dtos = entries
            .OrderByDescending(e => e.LastActivityAt)
            .Select(MapToDto)
            .ToList();

        if (dtos.Count == 0)
        {
            var libraryCount = await _userLibraryRepository
                .GetUserLibraryCountAsync(query.UserId, cancellationToken)
                .ConfigureAwait(false);
            var reason = libraryCount == 0
                ? GamebookEmptyReasons.NoEntries
                : GamebookEmptyReasons.FilterTooStrict;
            MeepleAiMetrics.GamebookIndexEmptyResponsesTotal.Add(1,
                new KeyValuePair<string, object?>("reason", reason));
        }

        return dtos;
    }

    /// <summary>
    /// Builds the canonical cache key for the gamebook index view of a user.
    /// BC-namespaced to avoid collision with other bounded contexts.
    /// </summary>
    internal static string BuildCacheKey(Guid userId) =>
        $"userlibrary:gamebooks:view:{userId}";

    /// <summary>
    /// Tag used for batch invalidation when user-scoped events fire
    /// (campaign created/deleted, library entry add/remove).
    /// </summary>
    internal static string BuildUserTag(Guid userId) =>
        $"user:{userId}";

    private static GamebookCardDataDto MapToDto(UserGamebookViewItem entry) => new(
        Id: entry.LibraryEntryId,
        GameId: entry.GameId,
        Title: entry.Title,
        Publisher: null,
        Year: entry.Year,
        Pages: entry.ReadyPdfCount,
        TotalPages: entry.ReadyPdfCount + entry.IndexingPdfCount + entry.FailedPdfCount,
        Chunks: entry.ChunkCount,
        Status: DeriveStatus(entry),
        Cover: entry.Cover,
        Emoji: null,
        QaCount: 0,
        SessionsCount: entry.SessionsCount,
        ErrorMsg: null);

    /// <summary>
    /// Derives the gamebook status badge value from PDF processing counts.
    ///
    /// Rules (AC-4):
    ///   - "ready"     → at least one PDF in Ready state (KB queryable)
    ///   - "indexing"  → no Ready PDFs but at least one in transition states
    ///   - "error"     → no Ready/Indexing PDFs but at least one Failed
    ///   - "ready"     → fallback (e.g. library entry with no PDFs yet, treated
    ///                   as ready so the card renders without warning UI)
    /// </summary>
    private static string DeriveStatus(UserGamebookViewItem entry)
    {
        if (entry.ReadyPdfCount > 0) return "ready";
        if (entry.IndexingPdfCount > 0) return "indexing";
        if (entry.FailedPdfCount > 0) return "error";
        return "ready";
    }
}

/// <summary>
/// Centralized constants for the <c>reason</c> label of
/// <see cref="MeepleAiMetrics.GamebookIndexEmptyResponsesTotal"/>.
///
/// Operational definitions (Issue #1292 AC-6.3):
///   - <see cref="NoEntries"/>     → user has zero UserLibraryEntries (legit empty)
///   - <see cref="FilterTooStrict"/> → user has library but none qualify as gamebook (UX signal)
///   - <see cref="BackendError"/>  → exception (not cancellation) during query (operational alert)
/// </summary>
internal static class GamebookEmptyReasons
{
    public const string NoEntries = "no_entries";
    public const string FilterTooStrict = "filter_too_strict";
    public const string BackendError = "backend_error";
}

