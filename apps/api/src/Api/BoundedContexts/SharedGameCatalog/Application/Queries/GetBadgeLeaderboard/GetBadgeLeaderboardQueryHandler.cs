using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;

/// <summary>
/// Handler for GetBadgeLeaderboardQuery.
/// Aggregates contribution stats and badge counts to generate leaderboard rankings.
/// Issue #2728: Application - Badge Assignment Handlers
/// </summary>
internal sealed class GetBadgeLeaderboardQueryHandler : IRequestHandler<GetBadgeLeaderboardQuery, List<LeaderboardEntryDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetBadgeLeaderboardQueryHandler> _logger;

    public GetBadgeLeaderboardQueryHandler(
        MeepleAiDbContext context,
        IUserRepository userRepository,
        ILogger<GetBadgeLeaderboardQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<LeaderboardEntryDto>> Handle(
        GetBadgeLeaderboardQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting badge leaderboard (Period={Period}, Page={Page}, PageSize={PageSize})",
            query.Period,
            query.PageNumber,
            query.PageSize);

        var periodStart = GetPeriodStart(query.Period);

        // Step 1: Get contribution counts per user
        var contributionCounts = await GetContributionCountsAsync(periodStart, cancellationToken)
            .ConfigureAwait(false);

        if (contributionCounts.Count == 0)
        {
            _logger.LogInformation("No contributions found for period {Period}", query.Period);
            return new List<LeaderboardEntryDto>();
        }

        var userIds = contributionCounts.Keys.ToList();

        // Step 2: Get badge counts and highest tier per user
        var badgeStats = await GetBadgeStatsAsync(userIds, cancellationToken)
            .ConfigureAwait(false);

        // Step 3: Combine data and rank
        var leaderboardData = contributionCounts.Select(kvp => new
        {
            UserId = kvp.Key,
            ContributionCount = kvp.Value,
            BadgeCount = badgeStats.TryGetValue(kvp.Key, out var stats) ? stats.BadgeCount : 0,
            HighestTier = badgeStats.TryGetValue(kvp.Key, out var tierStats) ? tierStats.HighestTier : Domain.ValueObjects.BadgeTier.Bronze,
            TopBadges = badgeStats.TryGetValue(kvp.Key, out var topStats) ? topStats.TopBadges : new List<UserBadgeDto>()
        })
        .OrderByDescending(x => x.ContributionCount)
        .ThenByDescending(x => x.BadgeCount)
        .ThenByDescending(x => x.HighestTier)
        .ToList();

        // Step 4: Apply pagination
        var pagedData = leaderboardData
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        // Step 5: Fetch user details
        var leaderboard = new List<LeaderboardEntryDto>();
        var rank = (query.PageNumber - 1) * query.PageSize + 1;

        foreach (var entry in pagedData)
        {
            var user = await _userRepository.GetByIdAsync(entry.UserId).ConfigureAwait(false);

            leaderboard.Add(new LeaderboardEntryDto
            {
                Rank = rank++,
                UserId = entry.UserId,
                UserName = user?.DisplayName ?? "Unknown",
                AvatarUrl = null, // User entity does not have avatar URL (future enhancement)
                ContributionCount = entry.ContributionCount,
                BadgeCount = entry.BadgeCount,
                HighestBadgeTier = entry.HighestTier,
                TopBadges = entry.TopBadges
            });
        }

        _logger.LogInformation(
            "Retrieved {Count} leaderboard entries for period {Period}",
            leaderboard.Count,
            query.Period);

        return leaderboard;
    }

    private async Task<Dictionary<Guid, int>> GetContributionCountsAsync(
        DateTime? periodStart,
        CancellationToken cancellationToken)
    {
        var query = _context.Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.Contributions)
            .AsQueryable();

        // Apply period filter to contributions
        if (periodStart.HasValue)
        {
            query = query.Where(c => c.Contributions.Any(cr => cr.ContributedAt >= periodStart.Value));
        }

        var contributors = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Group by user and count contributions
        return contributors
            .GroupBy(c => c.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(c => periodStart.HasValue
                    ? c.Contributions.Count(cr => cr.ContributedAt >= periodStart.Value)
                    : c.Contributions.Count));
    }

    private async Task<Dictionary<Guid, (int BadgeCount, Domain.ValueObjects.BadgeTier HighestTier, List<UserBadgeDto> TopBadges)>> GetBadgeStatsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var userBadges = await _context.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(ub => ub.Badge)
            .Where(ub => userIds.Contains(ub.UserId) && ub.RevokedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return userBadges
            .GroupBy(ub => ub.UserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var badgeCount = g.Count();
                    var highestTier = g.Max(ub => (Domain.ValueObjects.BadgeTier)ub.Badge!.Tier);
                    var topBadges = g
                        .OrderByDescending(ub => ub.Badge!.Tier)
                        .ThenBy(ub => ub.Badge!.DisplayOrder)
                        .Take(3)
                        .Select(ub => new UserBadgeDto
                        {
                            Id = ub.Id,
                            Code = ub.Badge!.Code,
                            Name = ub.Badge.Name,
                            Description = ub.Badge.Description,
                            IconUrl = ub.Badge.IconUrl,
                            Tier = (Domain.ValueObjects.BadgeTier)ub.Badge.Tier,
                            EarnedAt = ub.EarnedAt,
                            IsDisplayed = ub.IsDisplayed
                        })
                        .ToList();

                    return (badgeCount, highestTier, topBadges);
                });
    }

    private static DateTime? GetPeriodStart(LeaderboardPeriod period)
    {
        return period switch
        {
            LeaderboardPeriod.Week => DateTime.UtcNow.AddDays(-7),
            LeaderboardPeriod.Month => DateTime.UtcNow.AddDays(-30),
            LeaderboardPeriod.AllTime => null,
            _ => null
        };
    }
}
