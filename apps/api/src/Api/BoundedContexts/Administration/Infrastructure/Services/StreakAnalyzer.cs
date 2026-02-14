using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Analyzes user's activity streak to generate maintenance nudges.
/// </summary>
internal sealed class StreakAnalyzer : IStreakAnalyzer
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<StreakAnalyzer> _logger;
    private const int StreakAtRiskThreshold = 1; // Days since last activity
    private const int StreakBrokenThreshold = 2; // Days to consider streak broken
    private const int MinStreakToNudge = 3; // Minimum streak worth maintaining

    public StreakAnalyzer(
        MeepleAiDbContext dbContext,
        ILogger<StreakAnalyzer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<AIInsight>> AnalyzeStreakAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Calculate current streak from toolkit sessions
            var lastActivityDate = await _dbContext.Set<Session>()
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SessionDate)
                .Select(s => s.SessionDate)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (lastActivityDate == default)
            {
                // No activity yet - no streak to maintain
                return new List<AIInsight>();
            }

            var daysSinceActivity = (DateTime.UtcNow.Date - lastActivityDate.Date).Days;

            // Calculate current streak
            var currentStreak = await CalculateCurrentStreakAsync(userId, cancellationToken)
                .ConfigureAwait(false);

            var insights = new List<AIInsight>();

            // Streak at risk (user has been inactive for 1 day, but streak not broken yet)
            if (currentStreak >= MinStreakToNudge && daysSinceActivity == StreakAtRiskThreshold)
            {
                insights.Add(AIInsight.Create(
                    type: InsightType.StreakNudge,
                    title: $"Mantieni il tuo streak di {currentStreak} giorni!",
                    description: "Gioca oggi per mantenere la tua serie attiva. Non spezzare la catena!",
                    actionLabel: "Gioca Ora →",
                    actionUrl: "/toolkit",
                    priority: 9
                ));
            }
            // Streak recently broken (within last 2 days)
            else if (currentStreak >= MinStreakToNudge && daysSinceActivity == StreakBrokenThreshold)
            {
                insights.Add(AIInsight.Create(
                    type: InsightType.StreakNudge,
                    title: "Recupera il tuo streak!",
                    description: $"Avevi uno streak di {currentStreak} giorni. Ricomincia oggi!",
                    actionLabel: "Ricomincia →",
                    actionUrl: "/toolkit",
                    priority: 7
                ));
            }

            if (insights.Count > 0)
            {
                _logger.LogInformation(
                    "StreakAnalyzer generated {InsightCount} insights for user {UserId} (streak: {Streak}, days since activity: {Days})",
                    insights.Count,
                    userId,
                    currentStreak,
                    daysSinceActivity);
            }

            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error analyzing streak for user {UserId}",
                userId);
            return new List<AIInsight>(); // Graceful degradation
        }
    }

    /// <summary>
    /// Calculates the user's current consecutive day streak.
    /// </summary>
    private async Task<int> CalculateCurrentStreakAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.Set<Session>()
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SessionDate)
            .Select(s => s.SessionDate.Date)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sessions.Count == 0)
            return 0;

        int streak = 0;
        var expectedDate = DateTime.UtcNow.Date;

        foreach (var sessionDate in sessions)
        {
            if (sessionDate == expectedDate || sessionDate == expectedDate.AddDays(-1))
            {
                streak++;
                expectedDate = sessionDate.AddDays(-1);
            }
            else
            {
                break; // Streak broken
            }
        }

        return streak;
    }
}
