using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitRatings;

/// <summary>
/// Handler for <see cref="GetToolkitRatingsQuery"/>.
/// Wave 3 Phase 4b, PR #732 §5.3.3 / Issue #805.
/// </summary>
/// <remarks>
/// <para>
/// <b>Schema reality v1 carryover (Gate B)</b>: the <c>ToolkitRating</c> entity
/// does not exist in the current schema. Once the toolkit visibility check
/// passes the handler returns an empty stub envelope:
/// <c>items=[]</c>, <c>nextCursor=null</c>, <c>breakdown</c> all zero,
/// <c>averageStars=0</c>, <c>totalCount=0</c>. Wire shape is stable so the FE
/// ratings tab can render today and adopt real persistence in Phase 5 without
/// a fetch shape change.
/// </para>
///
/// <para>
/// <b>Visibility rule (PR #732 §5.2 security boundary)</b>: mirrors
/// <c>GetToolkitDetailQueryHandler</c> — non-authors only see published +
/// approved toolkits. Returning <c>null</c> here surfaces 404 at the endpoint
/// layer, preventing draft/yanked existence from leaking via the ratings
/// surface.
/// </para>
///
/// <para>
/// <b>Cursor</b>: with no persistence yet the cursor is always echoed-or-null;
/// any non-null input cursor is treated as "page 2 has nothing" → returns
/// <c>nextCursor=null</c>. The cursor format is intentionally opaque (base64
/// or token) so Phase 5 can choose its scheme without breaking the wire shape.
/// </para>
///
/// <para>
/// <b>Cache</b>: 5min HybridCache keyed by <c>toolkits:{toolkitId}:ratings:{cursor}:{limit}</c>
/// (PR #732 §3.2 caching matrix — short TTL because submissions land in
/// near-real-time once Phase 5 ships).
/// </para>
/// </remarks>
internal sealed class GetToolkitRatingsQueryHandler
    : IRequestHandler<GetToolkitRatingsQuery, ToolkitRatingsResponse?>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static readonly ToolkitRatingsBreakdownDto EmptyBreakdown =
        new(Star1: 0, Star2: 0, Star3: 0, Star4: 0, Star5: 0);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetToolkitRatingsQueryHandler> _logger;

    public GetToolkitRatingsQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetToolkitRatingsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolkitRatingsResponse?> Handle(
        GetToolkitRatingsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cursor = request.Cursor ?? "_";
        var cacheKey = $"toolkits:{request.ToolkitId}:ratings:{cursor}:{request.Limit}";

        var container = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(request, ct).ConfigureAwait(false),
            tags: [$"toolkit:{request.ToolkitId}", "ratings"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        return container.Response;
    }

    private async Task<ToolkitRatingsContainer> ComputeAsync(
        GetToolkitRatingsQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.GameToolkits
            .AsNoTracking()
            .Where(t => t.Id == request.ToolkitId)
            .Select(t => new
            {
                t.Id,
                t.CreatedByUserId,
                t.IsPublished,
                t.TemplateStatus
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} not found for ratings request (viewer={ViewerId})",
                request.ToolkitId,
                request.ViewerId);
            return new ToolkitRatingsContainer(null);
        }

        var isOwner = entity.CreatedByUserId == request.ViewerId;
        var isPublished = entity.IsPublished
            && (TemplateStatus)entity.TemplateStatus == TemplateStatus.Approved;

        // PR #732 §5.2 security boundary — non-authors must not learn that
        // a draft/yanked toolkit exists via the ratings surface.
        if (!isOwner && !isPublished)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} hidden from viewer {ViewerId} (isOwner=false, isPublished={IsPublished})",
                request.ToolkitId,
                request.ViewerId,
                isPublished);
            return new ToolkitRatingsContainer(null);
        }

        // Schema reality v1 carryover (Gate B): no ToolkitRating entity yet —
        // return empty stub envelope. Wire shape stable for FE.
        var response = new ToolkitRatingsResponse(
            Items: Array.Empty<ToolkitRatingDto>(),
            NextCursor: null,
            Breakdown: EmptyBreakdown,
            AverageStars: 0m,
            TotalCount: 0);

        _logger.LogInformation(
            "Returning empty ratings stub for toolkit {ToolkitId} (Gate B v1: ToolkitRating entity missing)",
            request.ToolkitId);

        return new ToolkitRatingsContainer(response);
    }

    /// <summary>
    /// Cache-friendly wrapper so we can store nullable visibility-denied results
    /// without HybridCache treating <c>null</c> as a cache miss.
    /// </summary>
    private sealed record ToolkitRatingsContainer(ToolkitRatingsResponse? Response);
}
