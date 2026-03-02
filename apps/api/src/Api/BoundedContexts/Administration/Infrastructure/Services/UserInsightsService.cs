using Microsoft.Extensions.Logging;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Orchestrates AI-powered user insights generation from multiple analyzers.
/// Executes analyzers in parallel for optimal performance.
/// </summary>
internal sealed class UserInsightsService : IUserInsightsService
{
    private readonly IBacklogAnalyzer _backlogAnalyzer;
    private readonly IRulesAnalyzer _rulesAnalyzer;
    private readonly IRAGRecommender _ragRecommender;
    private readonly IStreakAnalyzer _streakAnalyzer;
    private readonly ILogger<UserInsightsService> _logger;
    private const int MaxInsightsToReturn = 10;

    public UserInsightsService(
        IBacklogAnalyzer backlogAnalyzer,
        IRulesAnalyzer rulesAnalyzer,
        IRAGRecommender ragRecommender,
        IStreakAnalyzer streakAnalyzer,
        ILogger<UserInsightsService> logger)
    {
        _backlogAnalyzer = backlogAnalyzer ?? throw new ArgumentNullException(nameof(backlogAnalyzer));
        _rulesAnalyzer = rulesAnalyzer ?? throw new ArgumentNullException(nameof(rulesAnalyzer));
        _ragRecommender = ragRecommender ?? throw new ArgumentNullException(nameof(ragRecommender));
        _streakAnalyzer = streakAnalyzer ?? throw new ArgumentNullException(nameof(streakAnalyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<AIInsight>> GenerateInsightsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        _logger.LogInformation("Generating AI insights for user {UserId}", userId);

        var startTime = DateTime.UtcNow;

        // Execute all analyzers in parallel for performance
        var backlogTask = _backlogAnalyzer.AnalyzeBacklogAsync(userId, cancellationToken);
        var rulesTask = _rulesAnalyzer.AnalyzeRulebooksAsync(userId, cancellationToken);
        var ragTask = _ragRecommender.RecommendSimilarGamesAsync(userId, cancellationToken);
        var streakTask = _streakAnalyzer.AnalyzeStreakAsync(userId, cancellationToken);

        await Task.WhenAll(backlogTask, rulesTask, ragTask, streakTask)
            .ConfigureAwait(false);

        // ARCH-03: Use await instead of .Result to preserve exception fidelity
        var backlogInsights = await backlogTask.ConfigureAwait(false);
        var rulesInsights = await rulesTask.ConfigureAwait(false);
        var ragInsights = await ragTask.ConfigureAwait(false);
        var streakInsights = await streakTask.ConfigureAwait(false);

        var allInsights = new List<AIInsight>();
        allInsights.AddRange(backlogInsights);
        allInsights.AddRange(rulesInsights);
        allInsights.AddRange(ragInsights);
        allInsights.AddRange(streakInsights);

        // Sort by priority (descending) and limit to max count
        var sortedInsights = allInsights
            .OrderByDescending(i => i.Priority)
            .ThenByDescending(i => i.CreatedAt)
            .Take(MaxInsightsToReturn)
            .ToList();

        var duration = DateTime.UtcNow - startTime;

        _logger.LogInformation(
            "Generated {InsightCount} insights for user {UserId} in {DurationMs}ms (backlog:{BacklogCount}, rules:{RulesCount}, rag:{RagCount}, streak:{StreakCount})",
            sortedInsights.Count,
            userId,
            duration.TotalMilliseconds,
            backlogInsights.Count,
            rulesInsights.Count,
            ragInsights.Count,
            streakInsights.Count);

        return sortedInsights;
    }
}
