using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Gamification.Application.Services;

/// <summary>
/// Evaluates achievement rules by querying across bounded context tables.
/// Uses DbContext directly for cross-context read queries.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class AchievementRuleEvaluator : IAchievementRuleEvaluator
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<AchievementRuleEvaluator> _logger;

    public AchievementRuleEvaluator(
        MeepleAiDbContext context,
        ILogger<AchievementRuleEvaluator> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> EvaluateProgressAsync(
        string achievementCode,
        int threshold,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var currentValue = achievementCode switch
        {
            "FIRST_GAME" => await CountLibraryGamesAsync(userId, cancellationToken).ConfigureAwait(false),
            "COLLECTOR_100" => await CountLibraryGamesAsync(userId, cancellationToken).ConfigureAwait(false),
            "SESSION_MASTER" => await CountCompletedSessionsAsync(userId, cancellationToken).ConfigureAwait(false),
            "EXPLORER_10" => await CountDistinctGamesPlayedAsync(userId, cancellationToken).ConfigureAwait(false),
            "AI_EXPERT_50" => await CountChatSessionsAsync(userId, cancellationToken).ConfigureAwait(false),
            "SHARER" => await CountShareRequestsAsync(userId, cancellationToken).ConfigureAwait(false),
            "STREAK_7_DAYS" => await CalculateCurrentStreakAsync(userId, cancellationToken).ConfigureAwait(false),
            "STREAK_30_DAYS" => await CalculateCurrentStreakAsync(userId, cancellationToken).ConfigureAwait(false),
            "REVIEWER_5" => await CountLibraryNotesAsync(userId, cancellationToken).ConfigureAwait(false),
            "VETERAN_365" => await CalculateAccountAgeDaysAsync(userId, cancellationToken).ConfigureAwait(false),
            _ => 0
        };

        if (threshold <= 0) return 0;

        var progress = (int)Math.Min(100, (double)currentValue / threshold * 100);

        _logger.LogDebug(
            "Achievement {Code} for user {UserId}: {Current}/{Threshold} = {Progress}%",
            achievementCode, userId, currentValue, threshold, progress);

        return progress;
    }

    private async Task<int> CountLibraryGamesAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Set<UserLibraryEntryEntity>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    private async Task<int> CountCompletedSessionsAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Set<UserGameSessionEntity>()
            .AsNoTracking()
            .CountAsync(s => s.UserLibraryEntry != null && s.UserLibraryEntry.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    private async Task<int> CountDistinctGamesPlayedAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Set<UserGameSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserLibraryEntry != null && s.UserLibraryEntry.UserId == userId)
            .Select(s => s.UserLibraryEntryId)
            .Distinct()
            .CountAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<int> CountChatSessionsAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Set<ChatSessionEntity>()
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId && !c.IsArchived, ct)
            .ConfigureAwait(false);
    }

    private async Task<int> CountShareRequestsAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .CountAsync(sr => sr.UserId == userId && sr.Status == (int)ShareRequestStatus.Approved, ct)
            .ConfigureAwait(false);
    }

    private async Task<int> CalculateCurrentStreakAsync(Guid userId, CancellationToken ct)
    {
        // Get all play dates for the user in the last 60 days, ordered descending
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        var playDates = await _context.Set<UserGameSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserLibraryEntry != null
                        && s.UserLibraryEntry.UserId == userId
                        && s.PlayedAt >= sixtyDaysAgo)
            .Select(s => s.PlayedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (playDates.Count == 0) return 0;

        // Count consecutive days from today backwards
        var streak = 0;
        var expectedDate = DateTime.UtcNow.Date;

        foreach (var date in playDates)
        {
            if (date == expectedDate || date == expectedDate.AddDays(-1))
            {
                streak++;
                expectedDate = date.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private async Task<int> CountLibraryNotesAsync(Guid userId, CancellationToken ct)
    {
        return await _context.Set<UserLibraryEntryEntity>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.Notes != null && e.Notes.Length > 0, ct)
            .ConfigureAwait(false);
    }

    private async Task<int> CalculateAccountAgeDaysAsync(Guid userId, CancellationToken ct)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.CreatedAt })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (user == null) return 0;

        return (int)(DateTime.UtcNow - user.CreatedAt).TotalDays;
    }
}

/// <summary>
/// Status enum for share requests (matching int values in ShareRequestEntity).
/// </summary>
internal enum ShareRequestStatus
{
    Pending = 0,
    InReview = 1,
    ChangesRequested = 2,
    Approved = 3,
    Rejected = 4,
    Withdrawn = 5
}
