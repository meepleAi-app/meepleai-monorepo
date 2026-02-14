using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.Infrastructure;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Analyzes user's game library to detect backlog games (unplayed for 30+ days).
/// </summary>
internal sealed class BacklogAnalyzer : IBacklogAnalyzer
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<BacklogAnalyzer> _logger;
    private const int BacklogThresholdDays = 30;
    private const int MaxInsights = 3;

    public BacklogAnalyzer(
        MeepleAiDbContext dbContext,
        ILogger<BacklogAnalyzer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<AIInsight>> AnalyzeBacklogAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thresholdDate = DateTime.UtcNow.AddDays(-BacklogThresholdDays);

            // Find games added but never played or not played recently
            var backlogGames = await _dbContext.Set<UserLibraryEntry>()
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .Where(e => e.AddedAt < thresholdDate) // Game in library for 30+ days
                .Where(e => !e.Sessions.Any() || e.Sessions.Max(s => s.PlayedAt) < thresholdDate) // Not played or last play >30d ago
                .OrderBy(e => e.AddedAt) // Oldest first
                .Take(MaxInsights)
                .Select(e => new { e.Id, e.GameId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (backlogGames.Count == 0)
                return new List<AIInsight>();

            var insights = new List<AIInsight>();

            // Create aggregate insight (simplified - no game titles for MVP)
            if (backlogGames.Count > 1)
            {
                insights.Add(AIInsight.Create(
                    type: InsightType.BacklogAlert,
                    title: $"{backlogGames.Count} giochi nel backlog",
                    description: $"Hai {backlogGames.Count} giochi non giocati da oltre {BacklogThresholdDays} giorni. Riscopri la tua collezione!",
                    actionLabel: "Esplora Backlog →",
                    actionUrl: "/library?filter=unplayed&sort=oldest",
                    priority: 7
                ));
            }
            else if (backlogGames.Count == 1)
            {
                // Single game specific insight
                var game = backlogGames[0];
                insights.Add(AIInsight.Create(
                    type: InsightType.BacklogAlert,
                    title: "Gioco da riscoprire",
                    description: $"Hai un gioco non giocato da oltre {BacklogThresholdDays} giorni. Che ne dici di una partita?",
                    actionLabel: "Vedi Dettagli →",
                    actionUrl: $"/library/games/{game.GameId}",
                    priority: 6
                ));
            }

            _logger.LogInformation(
                "BacklogAnalyzer generated {InsightCount} insights for user {UserId}",
                insights.Count,
                userId);

            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error analyzing backlog for user {UserId}",
                userId);
            return new List<AIInsight>(); // Graceful degradation
        }
    }
}
