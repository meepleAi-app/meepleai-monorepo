using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// Handler for <see cref="GetRecommendedToolkitsQuery"/>.
/// Wave 3 Phase 4a, PR #732 §4.3.4 / Issue #805.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recommendation algorithm (Nygard §4.3.4 spec)</b>:
/// <c>(ratingAverage * log(ratingCount + 1)) DESC, installCount DESC tiebreak,
/// createdAt DESC final tiebreak</c>. The Bayesian-style score balances rating
/// quality against rating volume so a single 5-star toolkit doesn't outrank a
/// 4.5-star toolkit with hundreds of ratings.
/// </para>
///
/// <para>
/// <b>Schema reality v1 carryover (Gate B)</b>: there is no <c>ToolkitRating</c>
/// or <c>ToolkitInstallation</c> entity in the current schema, so all three
/// inputs to the score formula are 0/null in v1. The effective sort therefore
/// collapses to <c>createdAt DESC</c> — recent published toolkits surface first.
/// The wire shape is stable so the FE rail can render today and adopt the real
/// signals without a re-fetch shape change.
/// </para>
///
/// <para>
/// <b>Visibility filter</b>: only published toolkits surface
/// (<c>IsPublished == true &amp;&amp; TemplateStatus == Approved</c>). Drafts,
/// pending review, rejected, and yanked toolkits are excluded — non-authors
/// must not see them on a public discover surface (mirrors the security
/// boundary in <c>GetToolkitDetailQueryHandler</c>).
/// </para>
///
/// <para>
/// <b>Cache</b>: 30min HybridCache keyed by <c>discover:recommendedToolkits</c>
/// (PR #732 §3.2 caching matrix). Bulk-fetches author display names for the
/// trimmed slice (no N+1) using <c>UserEntity</c> lookup.
/// </para>
/// </remarks>
internal sealed class GetRecommendedToolkitsQueryHandler
    : IRequestHandler<GetRecommendedToolkitsQuery, RecommendedToolkitsResponse>
{
    private const string CacheKey = "discover:recommendedToolkits";
    private const int MaxCacheLimit = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetRecommendedToolkitsQueryHandler> _logger;

    public GetRecommendedToolkitsQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetRecommendedToolkitsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RecommendedToolkitsResponse> Handle(
        GetRecommendedToolkitsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allRecommended = await _cache.GetOrCreateAsync(
            CacheKey,
            async ct => await ComputeRecommendedAsync(MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["toolkits", "discover", "recommendedToolkits"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        var trimmed = allRecommended.Items
            .Take(request.Limit)
            .ToList();

        _logger.LogInformation(
            "Returning {Count} recommended toolkits (limit={Limit}) from cache/compute",
            trimmed.Count,
            request.Limit);

        return new RecommendedToolkitsResponse(trimmed);
    }

    private async Task<RecommendedToolkitsResponse> ComputeRecommendedAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        // Visibility filter (PR #732 §5.2 security boundary): only show published
        // + approved toolkits on the public discover surface.
        var approvedStatus = (int)TemplateStatus.Approved;

        // Schema reality v1 carryover (Gate B): rating/install/cover columns do
        // not exist on GameToolkitEntity, so we sort by CreatedAt DESC as the
        // effective fallback. The wire shape declares the full Bayesian score
        // formula so the sort upgrade is non-breaking.
        var rows = await _context.GameToolkits
            .AsNoTracking()
            .Where(t => t.IsPublished && t.TemplateStatus == approvedStatus)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.CreatedByUserId,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return new RecommendedToolkitsResponse(Array.Empty<RecommendedToolkitDto>());
        }

        // Bulk-fetch author display names for the slice (no N+1).
        var authorIds = rows.Select(r => r.CreatedByUserId).Distinct().ToList();
        var authors = await _context.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var authorMap = authors.ToDictionary(a => a.Id);

        var items = rows.Select(r =>
        {
            string authorName;
            if (authorMap.TryGetValue(r.CreatedByUserId, out var author))
            {
                authorName = !string.IsNullOrWhiteSpace(author.DisplayName)
                    ? author.DisplayName!
                    : (string.IsNullOrWhiteSpace(author.Email) ? "Unknown author" : author.Email);
            }
            else
            {
                // Defensive: author row was deleted (account removal).
                authorName = "Unknown author";
            }

            return new RecommendedToolkitDto(
                Id: r.Id,
                Name: r.Name,
                AuthorName: authorName,
                InstallCount: 0,
                RatingAverage: null,
                RatingCount: 0,
                CoverImageUrl: null);
        }).ToList();

        return new RecommendedToolkitsResponse(items);
    }
}
