using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;

/// <summary>
/// Handler for <see cref="GetTopContributorsQuery"/>.
/// Issue #593 (Wave A.3a) — spec §5.4.
///
/// <para>
/// Surfaces the top global contributors for the public `/shared-games` sidebar.
/// Score formula (mockup): <c>Score = TotalSessions + TotalWins * 2</c>.
/// </para>
///
/// <para>
/// Per spec §9 decision 4, <c>TotalSessions</c> / <c>TotalWins</c> count
/// <strong>ALL</strong> sessions — there is no filter on
/// <c>Game.ApprovalStatus</c>. Sessions tied to unpublished or private games
/// still count toward a user's score; reputation reflects play activity, not
/// catalog visibility.
/// </para>
///
/// <para>
/// Cross-BC read: this handler reads <c>GameSessions</c> /
/// <c>GameNightSessions</c> / <c>Users</c> (GameManagement &amp; Authentication
/// BCs) directly via <see cref="MeepleAiDbContext"/>. This is acceptable as a
/// read-only projection — no command crosses BC boundaries (spec §8 risk row 5).
/// </para>
///
/// <para>
/// Cache: HybridCache key <c>top-contributors:{limit}</c>, TTL L1 15min /
/// L2 1h, tag <c>top-contributors</c>. The tag is invalidated by the
/// <c>SessionCompletedForContributorsHandler</c> event handler (registered in
/// Commit follow-up #34) when a session completes — keeping the leaderboard
/// fresh without cold-querying on every request.
/// </para>
/// </summary>
internal sealed class GetTopContributorsQueryHandler
    : IRequestHandler<GetTopContributorsQuery, List<TopContributorDto>>
{
    /// <summary>
    /// HybridCache tag for batch invalidation. Kept in sync with the value
    /// used by <c>SessionCompletedForContributorsHandler</c>.
    /// </summary>
    internal const string CacheTag = "top-contributors";

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetTopContributorsQueryHandler> _logger;

    public GetTopContributorsQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetTopContributorsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<TopContributorDto>> Handle(
        GetTopContributorsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = query.Limit;
        var cacheKey = $"top-contributors:{limit}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(limit, ct).ConfigureAwait(false),
            tags: [CacheTag],
            expiration: CacheExpiration,
            ct: cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<TopContributorDto>> ComputeAsync(int limit, CancellationToken ct)
    {
        // Step 1: aggregate "completed sessions created by user" counts.
        // GameSessions.Status uses string values from SessionStatus VO ("Completed").
        var sessionCounts = await _context.GameSessions
            .AsNoTracking()
            .Where(s => s.CreatedByUserId.HasValue && s.Status == "Completed")
            .GroupBy(s => s.CreatedByUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Step 2: aggregate "completed game-night sessions where user was winner".
        // GameNightSessions.Status uses GameNightSessionStatus enum values ("Completed").
        var winCounts = await _context.GameNightSessions
            .AsNoTracking()
            .Where(gns => gns.WinnerId.HasValue && gns.Status == "Completed")
            .GroupBy(gns => gns.WinnerId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (sessionCounts.Count == 0 && winCounts.Count == 0)
        {
            _logger.LogInformation("No completed sessions found — top-contributors empty.");
            return new List<TopContributorDto>();
        }

        var sessionMap = sessionCounts.ToDictionary(x => x.UserId, x => x.Count);
        var winMap = winCounts.ToDictionary(x => x.UserId, x => x.Count);

        // Step 3: union userIds, compute scores, rank with deterministic tie-break.
        // Spec §7.2: ties broken by UserId ASC for stable pagination.
        // Over-fetch a buffer (limit * 2 + 10) to compensate for users filtered out
        // by privacy guards (suspended / non-Active / no DisplayName) in step 4.
        var overFetch = Math.Max(limit * 2, limit + 10);

        var ranked = sessionMap.Keys
            .Union(winMap.Keys)
            .Select(userId =>
            {
                var sessions = sessionMap.GetValueOrDefault(userId);
                var wins = winMap.GetValueOrDefault(userId);
                return new
                {
                    UserId = userId,
                    TotalSessions = sessions,
                    TotalWins = wins,
                    Score = sessions + wins * 2
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.UserId)
            .Take(overFetch)
            .ToList();

        // Step 4: fetch user profile details and apply privacy guards.
        //   - !IsSuspended  → don't surface suspended accounts on public widget.
        //   - Status == "Active" → exclude banned/deleted (Epic #4068).
        //   - DisplayName != null → spec §5.4 requires non-null DisplayName;
        //     users who never set a display name shouldn't leak email-derived
        //     identifiers on a public leaderboard.
        var candidateIds = ranked.Select(r => r.UserId).ToList();
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => candidateIds.Contains(u.Id)
                     && !u.IsSuspended
                     && u.Status == "Active"
                     && u.DisplayName != null)
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var userMap = users.ToDictionary(u => u.Id);

        // Step 5: build DTOs in rank order, drop users filtered out by privacy guards.
        var result = ranked
            .Where(r => userMap.ContainsKey(r.UserId))
            .Take(limit)
            .Select(r =>
            {
                var u = userMap[r.UserId];
                return new TopContributorDto(
                    UserId: r.UserId,
                    DisplayName: u.DisplayName!, // guarded non-null in step 4
                    AvatarUrl: u.AvatarUrl,
                    TotalSessions: r.TotalSessions,
                    TotalWins: r.TotalWins,
                    Score: r.Score);
            })
            .ToList();

        _logger.LogInformation(
            "Computed {Count} top-contributors from {SessionUsers} session aggregates and {WinUsers} win aggregates.",
            result.Count,
            sessionCounts.Count,
            winCounts.Count);

        return result;
    }
}
