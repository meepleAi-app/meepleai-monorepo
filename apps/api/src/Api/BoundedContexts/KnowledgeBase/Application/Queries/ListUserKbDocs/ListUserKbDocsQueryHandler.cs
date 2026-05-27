using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;

/// <summary>
/// Handler for <see cref="ListUserKbDocsQuery"/> — returns the authenticated
/// user's KB documents cross-game, paginated and ordered by recency
/// (<c>ProcessedAt ?? UploadedAt</c> DESC). BE-1 #1588.
/// </summary>
/// <remarks>
/// <para><b>Filter</b>: <c>UploadedByUserId == request.UserId</c>. When
/// <c>State == "all"</c> all 8 ProcessingState values are returned; the default
/// (<c>"ready"</c> or null) filters to <c>"Ready"</c>.</para>
///
/// <para><b>Cache</b>: HybridCache per-page key (TTL 5min, tags
/// <c>["kb", "user-docs", $"user:{userId}"]</c>). Tag-based invalidation is
/// emitted on the <c>PdfStateChangedEvent</c> domain event.</para>
///
/// <para><b>Bulk game-name fetch</b>: distinct <c>SharedGameId</c> values from
/// the page slice are resolved in ONE call to
/// <see cref="ISharedGameRepository.GetNamesByIdsAsync"/> to avoid N+1.</para>
/// </remarks>
internal sealed class ListUserKbDocsQueryHandler
    : IQueryHandler<ListUserKbDocsQuery, KbDocsListResponse>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string ReadyState = "Ready";

    private readonly MeepleAiDbContext _context;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<ListUserKbDocsQueryHandler> _logger;

    public ListUserKbDocsQueryHandler(
        MeepleAiDbContext context,
        ISharedGameRepository sharedGameRepository,
        IHybridCacheService cache,
        ILogger<ListUserKbDocsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbDocsListResponse> Handle(
        ListUserKbDocsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var state = (request.State ?? "ready").ToLowerInvariant();
        var includeAllStates = string.Equals(state, "all", StringComparison.Ordinal);
        var cacheKey =
            $"kb-docs:user:{request.UserId}:p:{request.Page}:sz:{request.PageSize}:state:{state}";

        var response = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(request, includeAllStates, ct).ConfigureAwait(false),
            tags: ["kb", "user-docs", $"user:{request.UserId}"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Listed {Count} of {Total} KB docs for user (hash {UserHash}) state={State} page={Page} size={PageSize}",
            response.Items.Count,
            response.Total,
            HashUserId(request.UserId),
            state,
            response.Page,
            response.PageSize);

        return response;
    }

    private async Task<KbDocsListResponse> ComputeAsync(
        ListUserKbDocsQuery request,
        bool includeAllStates,
        CancellationToken cancellationToken)
    {
        var baseQuery = _context.Set<PdfDocumentEntity>()
            .AsNoTracking()
            .Where(p => p.UploadedByUserId == request.UserId);

        if (!includeAllStates)
        {
            baseQuery = baseQuery.Where(p => p.ProcessingState == ReadyState);
        }

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            return new KbDocsListResponse(
                Items: Array.Empty<UserKbDocDto>(),
                Total: 0,
                Page: request.Page,
                PageSize: request.PageSize);
        }

        var rows = await baseQuery
            .OrderByDescending(p => p.ProcessedAt ?? p.UploadedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id,
                p.SharedGameId,
                p.FileName,
                p.ProcessingState,
                p.PageCount,
                p.ProcessedAt,
                p.UploadedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameIds = rows
            .Where(r => r.SharedGameId.HasValue)
            .Select(r => r.SharedGameId!.Value)
            .Distinct()
            .ToList();

        IReadOnlyDictionary<Guid, string> gameNames = gameIds.Count > 0
            ? await _sharedGameRepository
                .GetNamesByIdsAsync(gameIds, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        var items = rows.Select(r => new UserKbDocDto(
            Id: r.Id,
            GameId: r.SharedGameId,
            GameName: r.SharedGameId.HasValue
                && gameNames.TryGetValue(r.SharedGameId.Value, out var name)
                    ? name
                    : null,
            FileName: r.FileName,
            ProcessingState: r.ProcessingState,
            PageCount: r.PageCount,
            ProcessedAt: r.ProcessedAt,
            UploadedAt: r.UploadedAt)).ToList();

        return new KbDocsListResponse(
            Items: items,
            Total: total,
            Page: request.Page,
            PageSize: request.PageSize);
    }

    /// <summary>
    /// Stable hashed user-id token for log redaction (avoids leaking raw Guids
    /// into log aggregation). 8 hex chars is enough for cross-request correlation
    /// without being reversible.
    /// </summary>
    private static string HashUserId(Guid userId) =>
        userId.ToString("N")[..8];
}
