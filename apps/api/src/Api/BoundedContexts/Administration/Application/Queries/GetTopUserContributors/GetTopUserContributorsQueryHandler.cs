using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.GetTopUserContributors;

/// <summary>
/// Handler for <see cref="GetTopUserContributorsQuery"/>.
/// Wave 3 Phase 4a, PR #732 §4.3.6 / Issue #805.
/// </summary>
/// <remarks>
/// <para>
/// <b>V1 strategy (Fowler §4.3.6 spec)</b>: cross-BC SQL aggregation at request
/// time + 1h cache. V2 (deferred): background job + materialized view
/// (<c>top_contributors_daily</c> table) — out of scope for this issue.
/// </para>
///
/// <para>
/// <b>Schema reality v1 carryover (Gate B audit results)</b>:
/// <list type="bullet">
///   <item><c>PdfDocumentEntity.UploadedByUserId</c> ✅ exists — KB uploads count
///         is real.</item>
///   <item><c>GameFaqEntity</c> ❌ no creator FK column — FAQs count stubbed
///         to <c>0</c>. Documented in DTO XML doc.</item>
///   <item><c>AgentDefinition</c> ❌ no <c>OwnerUserId</c> / <c>CreatedByUserId</c>
///         column — agents-created count stubbed to <c>0</c>. Documented in DTO XML doc.</item>
/// </list>
/// In v1 the effective <c>ContributionCount</c> therefore equals
/// <c>KbUploadsCount</c>. The wire shape is stable so the FE rail can render
/// today and adopt the real metrics without a re-fetch shape change.
/// </para>
///
/// <para>
/// <b>Privacy guards</b>: skip suspended accounts, require <c>Status == "Active"</c>,
/// and require non-null <c>DisplayName</c> (mirrors the existing
/// <c>GetTopContributorsQueryHandler</c> in <c>SharedGameCatalog</c>). Email-derived
/// identifiers must not leak on a public discover surface.
/// </para>
///
/// <para>
/// <b>Cross-BC read</b>: this handler reads <c>PdfDocuments</c>
/// (DocumentProcessing) and <c>Users</c> (Authentication) directly via
/// <see cref="MeepleAiDbContext"/>. Acceptable as a read-only projection — no
/// command crosses BC boundaries.
/// </para>
///
/// <para>
/// <b>Cache</b>: 1h HybridCache keyed by <c>discover:topUserContributors</c>
/// (PR #732 §3.2 caching matrix — daily batch refresh slot for V2 migration).
/// </para>
/// </remarks>
internal sealed class GetTopUserContributorsQueryHandler
    : IRequestHandler<GetTopUserContributorsQuery, TopUserContributorsResponse>
{
    private const string CacheKey = "discover:topUserContributors";
    private const int MaxCacheLimit = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetTopUserContributorsQueryHandler> _logger;

    public GetTopUserContributorsQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetTopUserContributorsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TopUserContributorsResponse> Handle(
        GetTopUserContributorsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allContributors = await _cache.GetOrCreateAsync(
            CacheKey,
            async ct => await ComputeContributorsAsync(MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["users", "discover", "topUserContributors"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        var trimmed = allContributors.Items
            .Take(request.Limit)
            .ToList();

        _logger.LogInformation(
            "Returning {Count} top user contributors (limit={Limit}) from cache/compute",
            trimmed.Count,
            request.Limit);

        return new TopUserContributorsResponse(trimmed);
    }

    private async Task<TopUserContributorsResponse> ComputeContributorsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        // Step 1: aggregate KB uploads per user (the only real signal in v1).
        // PdfDocuments use DocumentProcessing BC; cross-BC read is acceptable
        // as a read-only projection per §4.3.6 V1 strategy.
        var uploadCounts = await _context.PdfDocuments
            .AsNoTracking()
            .Where(p => p.UploadedByUserId != Guid.Empty)
            .GroupBy(p => p.UploadedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (uploadCounts.Count == 0)
        {
            _logger.LogInformation("No PDF uploads found — top-contributors empty.");
            return new TopUserContributorsResponse(Array.Empty<TopUserContributorDto>());
        }

        // Step 2: rank by ContributionCount (= KbUploadsCount in v1) DESC.
        // Tie-break: UserId ASC for deterministic pagination.
        // Over-fetch buffer (limit * 2 + 10) to compensate for users filtered
        // out by privacy guards in step 3.
        var overFetch = Math.Max(limit * 2, limit + 10);
        var ranked = uploadCounts
            .Select(u => new
            {
                u.UserId,
                FaqsCount = 0,            // Schema reality v1 carryover (Gate B)
                KbUploadsCount = u.Count,
                AgentsCreatedCount = 0,    // Schema reality v1 carryover (Gate B)
                ContributionCount = u.Count
            })
            .OrderByDescending(x => x.ContributionCount)
            .ThenBy(x => x.UserId)
            .Take(overFetch)
            .ToList();

        // Step 3: fetch user profile details and apply privacy guards.
        // Privacy guards: skip suspended, require Active status, require
        // non-null DisplayName (mirror SharedGameCatalog GetTopContributors
        // pattern for consistency on public surfaces).
        var candidateIds = ranked.Select(r => r.UserId).ToList();
        var users = await _context.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => candidateIds.Contains(u.Id)
                     && !u.IsSuspended
                     && u.Status == "Active"
                     && u.DisplayName != null)
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var userMap = users.ToDictionary(u => u.Id);

        // Step 4: build DTOs in rank order, drop users filtered out by privacy guards.
        var items = ranked
            .Where(r => userMap.ContainsKey(r.UserId))
            .Take(limit)
            .Select(r =>
            {
                var u = userMap[r.UserId];
                return new TopUserContributorDto(
                    Id: r.UserId,
                    DisplayName: u.DisplayName!, // guarded non-null in step 3
                    AvatarUrl: u.AvatarUrl,
                    ContributionCount: r.ContributionCount,
                    Breakdown: new TopUserContributorBreakdownDto(
                        FaqsCount: r.FaqsCount,
                        KbUploadsCount: r.KbUploadsCount,
                        AgentsCreatedCount: r.AgentsCreatedCount));
            })
            .ToList();

        _logger.LogInformation(
            "Computed {Count} top contributors from {UploadUsers} upload aggregates (Gate B v1: only KB uploads count).",
            items.Count,
            uploadCounts.Count);

        return new TopUserContributorsResponse(items);
    }
}
