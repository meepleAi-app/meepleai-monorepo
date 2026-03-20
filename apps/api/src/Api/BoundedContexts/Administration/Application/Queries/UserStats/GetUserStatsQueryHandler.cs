using Api.BoundedContexts.Administration.Application.Queries.UserStats;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Queries.UserStats;

/// <summary>
/// Handler for GetUserStatsQuery (Issue #4578)
/// Calculates dashboard statistics from multiple sources
/// </summary>
internal class GetUserStatsQueryHandler : IRequestHandler<GetUserStatsQuery, UserStatsDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetUserStatsQueryHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task<UserStatsDto> Handle(GetUserStatsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = GetCurrentUserId();
        var cacheKey = $"user-stats:{userId}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var now = DateTime.UtcNow;
                var thirtyDaysAgo = now.AddDays(-30);
                var sixtyDaysAgo = now.AddDays(-60);
                var sevenDaysAgo = now.AddDays(-7);

                // Total games in user library
                var totalGames = await _context.Set<UserLibraryEntryEntity>()
                    .CountAsync(e => e.UserId == userId, cancel)
                    .ConfigureAwait(false);

                // Monthly plays (last 30 days)
                var monthlyPlays = await _context.PlayRecords
                    .CountAsync(pr => pr.CreatedByUserId == userId && pr.SessionDate >= thirtyDaysAgo, cancel)
                    .ConfigureAwait(false);

                // Previous month plays (30-60 days ago)
                var previousMonthPlays = await _context.PlayRecords
                    .CountAsync(pr => pr.CreatedByUserId == userId
                        && pr.SessionDate >= sixtyDaysAgo
                        && pr.SessionDate < thirtyDaysAgo, cancel)
                    .ConfigureAwait(false);

                // Calculate percentage change
                var monthlyPlaysChange = previousMonthPlays > 0
                    ? (int)Math.Round(((double)(monthlyPlays - previousMonthPlays) / previousMonthPlays) * 100)
                    : 0;

                // Weekly play time (last 7 days)
                var weeklyDurations = await _context.PlayRecords
                    .Where(pr => pr.CreatedByUserId == userId && pr.SessionDate >= sevenDaysAgo)
                    .Where(pr => pr.Duration != null)
                    .Select(pr => pr.Duration!.Value)
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                var weeklyPlayTime = weeklyDurations.Aggregate(TimeSpan.Zero, (sum, dur) => sum + dur);

                // Monthly favorites (games played with high average scores last 30 days)
                var monthlyFavorites = await _context.PlayRecords
                    .Where(pr => pr.CreatedByUserId == userId && pr.SessionDate >= thirtyDaysAgo)
                    .Where(pr => pr.Players.SelectMany(p => p.Scores).Any())
                    .Select(pr => new
                    {
                        pr.GameName,
                        AvgScore = pr.Players.SelectMany(p => p.Scores).Average(s => s.Value)
                    })
                    .Where(x => x.AvgScore >= 90) // High score threshold
                    .Select(x => x.GameName)
                    .Distinct()
                    .CountAsync(cancel)
                    .ConfigureAwait(false);

                return new UserStatsDto(
                    TotalGames: totalGames,
                    MonthlyPlays: monthlyPlays,
                    MonthlyPlaysChange: monthlyPlaysChange,
                    WeeklyPlayTime: weeklyPlayTime,
                    MonthlyFavorites: monthlyFavorites);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            },
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private Guid GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context available");

        var userIdClaim = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        return Guid.Parse(userIdClaim);
    }
}
