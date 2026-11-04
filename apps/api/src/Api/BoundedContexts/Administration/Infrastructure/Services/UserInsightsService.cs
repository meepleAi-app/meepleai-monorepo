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

        // 🔴 Sequenziale, non `Task.WhenAll`, e qui la ragione è più insidiosa che un 500.
        // Tutti e quattro gli analyzer iniettano il `MeepleAiDbContext` **scoped** della richiesta
        // (BacklogAnalyzer:15, RulesAnalyzer:15, RAGRecommender:20, StreakAnalyzer:15), e un
        // DbContext non è thread-safe: avviarli insieme fa lanciare `ConcurrencyDetector`.
        //
        // Ma ciascun analyzer ha il proprio `try/catch` che torna una lista vuota, e
        // `AiInsightsService` ri-cattura in nome della «graceful degradation». Quindi il difetto
        // NON produce un 500 che qualcuno noterebbe: produce **insight mancanti in silenzio**, e
        // `/api/v1/dashboard/insights` risponde 200 con meno dati di quelli che avrebbe. È la
        // forma di #3843 — un'eccezione ingoiata che si presenta come assenza.
        //
        // Trovato come fratello latente mentre si verificava la correzione di #4059 su
        // `/admin/kb/pipeline/health`: lì la stessa causa dà 500 perché nulla la cattura. Non
        // riprodotto su questa rotta proprio perché i catch lo nascondono — e questo è l'argomento
        // per correggerlo, non per rinviarlo.
        //
        // Il parallelismo non era un guadagno: le quattro query vanno sulla stessa connessione e
        // il pool le serializza comunque. Se servisse davvero, un contesto per analyzer via
        // `IDbContextFactory`, non `WhenAll` su quello condiviso.
        var backlogInsights = await _backlogAnalyzer
            .AnalyzeBacklogAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        var rulesInsights = await _rulesAnalyzer
            .AnalyzeRulebooksAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        var ragInsights = await _ragRecommender
            .RecommendSimilarGamesAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        var streakInsights = await _streakAnalyzer
            .AnalyzeStreakAsync(userId, cancellationToken)
            .ConfigureAwait(false);

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
