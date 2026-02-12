using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Service for generating AI-powered insights for dashboard.
/// Issue #3916: Rule-based recommendations, backlog detection, rules reminders.
/// Uses rule-based fallback for game recommendations (RAG integration future enhancement).
/// </summary>
internal class AiInsightsService : IAiInsightsService
{
    private readonly IUserLibraryRepository _libraryRepo;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AiInsightsService> _logger;

    private const int BacklogDaysThreshold = 30;
    private const int RecentGamesLimit = 5;
    private const int RecommendationsLimit = 3;
    private const int BacklogLimit = 5;
    private const int RulesRemindersLimit = 3;

    public AiInsightsService(
        IUserLibraryRepository libraryRepo,
        MeepleAiDbContext dbContext,
        ILogger<AiInsightsService> logger)
    {
        _libraryRepo = libraryRepo ?? throw new ArgumentNullException(nameof(libraryRepo));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiInsightsDto> GetInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var insights = new List<DashboardInsightDto>();

        try
        {
            // Parallel execution for better performance
            var recommendationsTask = GetRecommendationsAsync(userId, cancellationToken);
            var backlogTask = GetBacklogAlertsAsync(userId, cancellationToken);
            var remindersTask = GetRulesRemindersAsync(userId, cancellationToken);

            await Task.WhenAll(recommendationsTask, backlogTask, remindersTask).ConfigureAwait(false);

            insights.AddRange(await recommendationsTask.ConfigureAwait(false));
            insights.AddRange(await backlogTask.ConfigureAwait(false));
            insights.AddRange(await remindersTask.ConfigureAwait(false));

            _logger.LogInformation(
                "Generated {Count} AI insights for user {UserId}",
                insights.Count,
                userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating AI insights for user {UserId}",
                userId);

            // Return empty insights on error (graceful degradation)
            return new AiInsightsDto(
                Array.Empty<DashboardInsightDto>(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(15));
        }

        return new AiInsightsDto(
            insights.AsReadOnly(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(15));
    }

    /// <summary>
    /// Gets game recommendations using rule-based approach.
    /// Future enhancement: RAG-based similar game query with vector embeddings.
    /// </summary>
    private async Task<List<DashboardInsightDto>> GetRecommendationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<DashboardInsightDto>();

        // Get user's recently played games
        var recentGames = await _libraryRepo.GetRecentlyPlayedAsync(
            userId,
            RecentGamesLimit,
            cancellationToken).ConfigureAwait(false);

        if (recentGames.Count == 0)
        {
            _logger.LogDebug("No recent games found for user {UserId}", userId);
            return recommendations;
        }

        // Get user's library game IDs to exclude from recommendations
        var libraryGameIds = await _dbContext.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.GameId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Rule-based recommendation: Simply recommend highly-rated games not in library
        // Future: Use category/mechanic overlap for better similarity
        var similarGames = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted &&
                        !libraryGameIds.Contains(g.Id) &&
                        g.AverageRating != null &&
                        g.AverageRating >= 7.0m)
            .OrderByDescending(g => g.AverageRating)
            .Take(RecommendationsLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Map to insights
        foreach (var game in similarGames)
        {
            var insight = new DashboardInsightDto(
                Id: $"recommendation-{game.Id}",
                Type: InsightType.Recommendation,
                Icon: "🎲",
                Title: game.Title,
                Description: $"Similar to games you've played recently. Try this highly-rated game!",
                ActionUrl: $"/games/{game.Id}",
                ActionLabel: "View Game",
                Priority: 1,
                Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["gameId"] = game.Id.ToString(),
                    ["averageRating"] = game.AverageRating ?? 0m,
                    ["confidence"] = 0.65 // Rule-based confidence (~65%)
                });

            recommendations.Add(insight);
        }

        _logger.LogDebug(
            "Generated {Count} rule-based recommendations for user {UserId}",
            recommendations.Count,
            userId);

        return recommendations;
    }

    /// <summary>
    /// Gets backlog alerts for games not played for 30+ days.
    /// </summary>
    private async Task<List<DashboardInsightDto>> GetBacklogAlertsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var alerts = new List<DashboardInsightDto>();

        // Get unplayed games (30+ days or never played)
        var unplayedGames = await _libraryRepo.GetUnplayedGamesAsync(
            userId,
            BacklogDaysThreshold,
            cancellationToken).ConfigureAwait(false);

        // Limit to top N backlog alerts
        var backlogGames = unplayedGames.Take(BacklogLimit);

        foreach (var entry in backlogGames)
        {
            // Get game details from SharedGame
            var game = await _dbContext.SharedGames
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == entry.GameId, cancellationToken)
                .ConfigureAwait(false);

            if (game == null) continue;

            var insight = new DashboardInsightDto(
                Id: $"backlog-{entry.Id}",
                Type: InsightType.Backlog,
                Icon: "⏰",
                Title: $"Time to play {game.Title}?",
                Description: $"This game has been in your library for a while. Schedule a game session!",
                ActionUrl: $"/games/{entry.GameId}",
                ActionLabel: "Schedule Session",
                Priority: 2,
                Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["gameId"] = entry.GameId.ToString(),
                    ["suggestedAction"] = "schedule_session"
                });

            alerts.Add(insight);
        }

        _logger.LogDebug(
            "Generated {Count} backlog alerts for user {UserId}",
            alerts.Count,
            userId);

        return alerts;
    }

    /// <summary>
    /// Gets rules reminders from saved chats with "regole" (rules) in title.
    /// </summary>
    private async Task<List<DashboardInsightDto>> GetRulesRemindersAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var reminders = new List<DashboardInsightDto>();

        // Query chats with "regole" or "rules" in title (case-insensitive)
        // Note: ChatThreadEntity doesn't have IsDeleted, uses Status field
        var rulesChats = await _dbContext.ChatThreads
            .AsNoTracking()
            .Where(c => c.UserId == userId &&
                        c.Status == "active" &&
                        (EF.Functions.ILike(c.Title, "%regole%") ||
                         EF.Functions.ILike(c.Title, "%rules%")))
            .OrderByDescending(c => c.CreatedAt)
            .Take(RulesRemindersLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Map to insights
        foreach (var chat in rulesChats)
        {
            // Extract game name from chat title if present
            var gameName = ExtractGameNameFromChatTitle(chat.Title);

            var insight = new DashboardInsightDto(
                Id: $"rules-{chat.Id}",
                Type: InsightType.RulesReminder,
                Icon: "📖",
                Title: chat.Title ?? "Rules Reminder",
                Description: $"You saved rules information for {gameName ?? "a game"}. Review before your next session!",
                ActionUrl: $"/chats/{chat.Id}",
                ActionLabel: "Review Rules",
                Priority: 3,
                Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["chatId"] = chat.Id.ToString(),
                    ["savedAt"] = chat.CreatedAt.ToString("O"),
                    ["gameName"] = gameName ?? "Unknown"
                });

            reminders.Add(insight);
        }

        _logger.LogDebug(
            "Generated {Count} rules reminders for user {UserId}",
            reminders.Count,
            userId);

        return reminders;
    }

    /// <summary>
    /// Extracts game name from chat title (heuristic: assumes format "Game Name - Topic" or "Regole: Game Name").
    /// </summary>
    private static string? ExtractGameNameFromChatTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        // Try pattern: "Regole Game Name" or "Rules Game Name"
        if (title.Contains("Regole:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = title.Split(':', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? parts[1].Split(['-'], 2, StringSplitOptions.TrimEntries)[0] : null;
        }

        if (title.Contains("Rules:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = title.Split(':', 2, StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? parts[1].Split(['-'], 2, StringSplitOptions.TrimEntries)[0] : null;
        }

        // Try pattern: "Game Name - Topic"
        var dashParts = title.Split(['-'], 2, StringSplitOptions.TrimEntries);
        return dashParts.Length == 2 ? dashParts[0] : null;
    }
}

/// <summary>
/// DTO for AI insights response (alias for DashboardInsightsResponseDto).
/// </summary>
internal record AiInsightsDto(
    IReadOnlyList<DashboardInsightDto> Insights,
    DateTime GeneratedAt,
    DateTime NextRefresh);
