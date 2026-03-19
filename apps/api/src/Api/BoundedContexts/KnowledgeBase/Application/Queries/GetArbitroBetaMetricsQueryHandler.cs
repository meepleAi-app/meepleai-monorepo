using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetArbitroBetaMetricsQuery - aggregates beta testing metrics.
/// Issue #4328: Arbitro Agent Beta Testing - Performance Monitoring Dashboard.
/// </summary>
internal sealed class GetArbitroBetaMetricsQueryHandler : IRequestHandler<GetArbitroBetaMetricsQuery, ArbitroBetaMetricsDto>
{
    private readonly IArbitroValidationFeedbackRepository _feedbackRepository;
    private readonly IRuleConflictFaqRepository _faqRepository;
    private readonly ILogger<GetArbitroBetaMetricsQueryHandler> _logger;

    public GetArbitroBetaMetricsQueryHandler(
        IArbitroValidationFeedbackRepository feedbackRepository,
        IRuleConflictFaqRepository faqRepository,
        ILogger<GetArbitroBetaMetricsQueryHandler> logger)
    {
        _feedbackRepository = feedbackRepository ?? throw new ArgumentNullException(nameof(feedbackRepository));
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ArbitroBetaMetricsDto> Handle(
        GetArbitroBetaMetricsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Generating Arbitro beta metrics: sessionFilter={SessionId}, dateRange={From}-{To}",
            request.GameSessionId,
            request.FromDate,
            request.ToDate);

        // 1. Get base accuracy metrics
        var (total, correct, _, _, avgRating) =
            await _feedbackRepository.GetAccuracyMetricsAsync(
                request.GameSessionId,
                request.FromDate,
                cancellationToken).ConfigureAwait(false);

        if (total == 0)
        {
            return CreateEmptyMetrics();
        }

        // 2. Get all feedbacks for detailed analysis
        var allFeedbacks = request.GameSessionId.HasValue
            ? await _feedbackRepository.GetBySessionIdAsync(request.GameSessionId.Value, cancellationToken).ConfigureAwait(false)
            : await _feedbackRepository.GetByUserIdAsync(Guid.Empty, limit: 1000, cancellationToken).ConfigureAwait(false);

        // Apply date filter if specified
        if (request.FromDate.HasValue)
        {
            allFeedbacks = allFeedbacks.Where(f => f.SubmittedAt >= request.FromDate.Value).ToList();
        }
        if (request.ToDate.HasValue)
        {
            allFeedbacks = allFeedbacks.Where(f => f.SubmittedAt <= request.ToDate.Value).ToList();
        }

        // 3. Calculate decision distribution
        var decisionDistribution = CalculateDecisionDistribution(allFeedbacks);

        // 4. Calculate conflict resolution accuracy
        var conflictAccuracy = CalculateConflictResolutionAccuracy(allFeedbacks);

        // 5. Get FAQ statistics
        var faqStats = await CalculateFaqStatisticsAsync(request.GameSessionId, cancellationToken).ConfigureAwait(false);

        // 6. Analyze violated rules (future enhancement - requires logging violated rules in feedback)
        var topViolatedRules = new List<ViolatedRuleStatsDto>(); // Placeholder

        // 7. Calculate accuracy trend
        var accuracyTrend = CalculateAccuracyTrend(allFeedbacks);

        var metrics = new ArbitroBetaMetricsDto
        {
            TotalFeedback = total,
            AccuracyPercentage = total > 0 ? (correct / (double)total) * 100 : 0,
            ConflictResolutionAccuracy = conflictAccuracy,
            AverageRating = avgRating,
            DecisionDistribution = decisionDistribution,
            FaqStatistics = faqStats,
            TopViolatedRules = topViolatedRules,
            AccuracyTrend = accuracyTrend
        };

        _logger.LogInformation(
            "Beta metrics generated: totalFeedback={Total}, accuracy={Accuracy:F2}%, conflictAccuracy={ConflictAccuracy:F2}%, avgRating={Rating:F2}",
            total,
            metrics.AccuracyPercentage,
            conflictAccuracy,
            avgRating);

        return metrics;
    }

    private static DecisionDistributionDto CalculateDecisionDistribution(IReadOnlyList<Domain.Entities.ArbitroValidationFeedback> feedbacks)
    {
        var validCount = feedbacks.Count(f => string.Equals(f.AiDecision, "VALID", StringComparison.Ordinal));
        var invalidCount = feedbacks.Count(f => string.Equals(f.AiDecision, "INVALID", StringComparison.Ordinal));
        var uncertainCount = feedbacks.Count(f => string.Equals(f.AiDecision, "UNCERTAIN", StringComparison.Ordinal));
        var total = feedbacks.Count;

        return new DecisionDistributionDto
        {
            ValidCount = validCount,
            InvalidCount = invalidCount,
            UncertainCount = uncertainCount,
            ValidPercentage = total > 0 ? (validCount / (double)total) * 100 : 0,
            InvalidPercentage = total > 0 ? (invalidCount / (double)total) * 100 : 0,
            UncertainPercentage = total > 0 ? (uncertainCount / (double)total) * 100 : 0
        };
    }

    private static double CalculateConflictResolutionAccuracy(IReadOnlyList<Domain.Entities.ArbitroValidationFeedback> feedbacks)
    {
        var conflictCases = feedbacks.Where(f => f.HadConflicts).ToList();
        if (conflictCases.Count == 0)
            return 100.0; // No conflicts = perfect accuracy

        var correctConflictResolutions = conflictCases.Count(f => f.Accuracy == Domain.Entities.AccuracyAssessment.Correct);
        return (correctConflictResolutions / (double)conflictCases.Count) * 100;
    }

    private async Task<FaqStatisticsDto> CalculateFaqStatisticsAsync(Guid? gameSessionId, CancellationToken cancellationToken)
    {
        // Get all FAQ entries (future: filter by game if needed)
        var allFaqs = gameSessionId.HasValue
            ? await _faqRepository.GetByGameIdAsync(gameSessionId.Value, cancellationToken).ConfigureAwait(false)
            : new List<GameManagement.Domain.Entities.RuleConflictFAQ>();

        var totalFaqHits = allFaqs.Sum(f => f.UsageCount);
        var totalValidations = await _feedbackRepository.GetAccuracyMetricsAsync(gameSessionId, since: null, cancellationToken).ConfigureAwait(false);
        var totalLlmCalls = totalValidations.total - totalFaqHits;

        var topFaqEntries = allFaqs
            .OrderByDescending(f => f.UsageCount)
            .Take(10)
            .Select(f => new TopFaqEntryDto
            {
                FaqId = f.Id,
                Pattern = f.Pattern,
                UsageCount = f.UsageCount,
                SuccessRate = 100.0 // Placeholder - requires feedback correlation
            })
            .ToList();

        return new FaqStatisticsDto
        {
            TotalFaqHits = totalFaqHits,
            TotalLlmCalls = Math.Max(0, totalLlmCalls),
            FaqHitRate = totalValidations.total > 0 ? (totalFaqHits / (double)totalValidations.total) * 100 : 0,
            TopFaqEntries = topFaqEntries
        };
    }

    private static List<AccuracyTrendPointDto> CalculateAccuracyTrend(IReadOnlyList<Domain.Entities.ArbitroValidationFeedback> feedbacks)
    {
        // Group by date and calculate daily accuracy
        var dailyGroups = feedbacks
            .GroupBy(f => f.SubmittedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var dayFeedbacks = g.ToList();
                var correctCount = dayFeedbacks.Count(f => f.Accuracy == Domain.Entities.AccuracyAssessment.Correct);
                var avgConfidence = dayFeedbacks.Average(f => f.AiConfidence);

                return new AccuracyTrendPointDto
                {
                    Date = g.Key,
                    FeedbackCount = dayFeedbacks.Count,
                    AccuracyPercentage = dayFeedbacks.Count > 0 ? (correctCount / (double)dayFeedbacks.Count) * 100 : 0,
                    AverageConfidence = avgConfidence
                };
            })
            .ToList();

        return dailyGroups;
    }

    private static ArbitroBetaMetricsDto CreateEmptyMetrics()
    {
        return new ArbitroBetaMetricsDto
        {
            TotalFeedback = 0,
            AccuracyPercentage = 0,
            ConflictResolutionAccuracy = 0,
            AverageRating = 0,
            DecisionDistribution = new DecisionDistributionDto
            {
                ValidCount = 0,
                InvalidCount = 0,
                UncertainCount = 0,
                ValidPercentage = 0,
                InvalidPercentage = 0,
                UncertainPercentage = 0
            },
            FaqStatistics = new FaqStatisticsDto
            {
                TotalFaqHits = 0,
                TotalLlmCalls = 0,
                FaqHitRate = 0,
                TopFaqEntries = new List<TopFaqEntryDto>()
            },
            TopViolatedRules = new List<ViolatedRuleStatsDto>(),
            AccuracyTrend = new List<AccuracyTrendPointDto>()
        };
    }
}
