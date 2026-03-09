using Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Retrieves structured data from RulebookAnalysis fields based on classified query intent.
/// Queries KeyConcepts, GeneratedFaqs, VictoryConditions, and KeyMechanics by intent type.
/// Issue #5453: Structured RAG fusion.
/// </summary>
internal sealed class StructuredRetrievalService : IStructuredRetrievalService
{
    private const double HighConfidenceThreshold = 0.85;

    private readonly IRulebookAnalysisRepository _repository;
    private readonly StructuredQueryIntentClassifier _classifier;
    private readonly ILogger<StructuredRetrievalService> _logger;

    public StructuredRetrievalService(
        IRulebookAnalysisRepository repository,
        StructuredQueryIntentClassifier classifier,
        ILogger<StructuredRetrievalService> logger)
    {
        _repository = repository;
        _classifier = classifier;
        _logger = logger;
    }

    public async Task<StructuredRetrievalResponse> RetrieveAsync(
        string query,
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var classification = _classifier.Classify(query);

        _logger.LogDebug(
            "Query intent classified: {Intent} (confidence: {Confidence:F2}) for game {GameId}",
            classification.Intent, classification.Confidence, sharedGameId);

        // General intent → no structured retrieval
        if (classification.Intent == StructuredQueryIntent.General)
        {
            return new StructuredRetrievalResponse(
                Results: [],
                Classification: classification,
                ShouldBypassVector: false,
                StructuredContributionPercent: 0.0);
        }

        // Get active analyses for this game
        var analyses = await _repository.GetBySharedGameIdAsync(sharedGameId, cancellationToken)
            .ConfigureAwait(false);

        var activeAnalyses = analyses.Where(a => a.IsActive).ToList();
        if (activeAnalyses.Count == 0)
        {
            _logger.LogDebug("No active RulebookAnalysis found for game {GameId}", sharedGameId);
            return new StructuredRetrievalResponse(
                Results: [],
                Classification: classification,
                ShouldBypassVector: false,
                StructuredContributionPercent: 0.0);
        }

        var results = new List<StructuredRetrievalResult>();

        foreach (var analysis in activeAnalyses)
        {
            var intentResults = classification.Intent switch
            {
                StructuredQueryIntent.VictoryConditions => RetrieveVictoryConditions(analysis, query),
                StructuredQueryIntent.Mechanics => RetrieveMechanics(analysis, query),
                StructuredQueryIntent.Glossary => RetrieveGlossary(analysis, query, classification.MatchedTerm),
                StructuredQueryIntent.Faq => RetrieveFaqs(analysis, query),
                _ => []
            };

            results.AddRange(intentResults);
        }

        // Sort by confidence descending
        results = results.OrderByDescending(r => r.Confidence).ToList();

        // Determine fusion behavior
        var maxConfidence = results.Count > 0 ? results.Max(r => r.Confidence) : 0.0;
        var shouldBypass = maxConfidence >= HighConfidenceThreshold && results.Count > 0;
        var structuredContribution = results.Count > 0
            ? Math.Min(100.0, maxConfidence * 100.0)
            : 0.0;

        _logger.LogInformation(
            "Structured retrieval: {Count} results, maxConfidence={MaxConf:F2}, bypass={Bypass}, contribution={Pct:F0}% for query '{Query}'",
            results.Count, maxConfidence, shouldBypass, structuredContribution, query.Length > 80 ? query[..80] + "..." : query);

        return new StructuredRetrievalResponse(
            Results: results,
            Classification: classification,
            ShouldBypassVector: shouldBypass,
            StructuredContributionPercent: structuredContribution);
    }

    private static List<StructuredRetrievalResult> RetrieveVictoryConditions(
        RulebookAnalysis analysis, string query)
    {
        var results = new List<StructuredRetrievalResult>();

        if (analysis.VictoryConditions is not null)
        {
            var vc = analysis.VictoryConditions;
            var content = $"Victory Conditions: {vc.Primary}";

            if (vc.Alternatives.Count > 0)
                content += $"\nAlternative ways to win: {string.Join("; ", vc.Alternatives)}";

            if (vc.IsPointBased && vc.TargetPoints.HasValue)
                content += $"\nThis is a point-based game. Target: {vc.TargetPoints} points.";

            results.Add(new StructuredRetrievalResult(
                Content: content,
                Confidence: 0.92,
                SourceIntent: StructuredQueryIntent.VictoryConditions,
                SourceField: "VictoryConditions",
                SharedGameId: analysis.SharedGameId));
        }

        return results;
    }

    private static List<StructuredRetrievalResult> RetrieveMechanics(
        RulebookAnalysis analysis, string query)
    {
        if (analysis.KeyMechanics.Count == 0)
            return [];

        var content = $"Key Game Mechanics: {string.Join(", ", analysis.KeyMechanics)}";

        // Also include game phases if available (related to mechanics)
        if (analysis.GamePhases.Count > 0)
        {
            var phases = analysis.GamePhases
                .OrderBy(p => p.Order)
                .Select(p => $"{p.Name}: {p.Description}");
            content += $"\n\nGame Phases:\n{string.Join("\n", phases)}";
        }

        return
        [
            new StructuredRetrievalResult(
                Content: content,
                Confidence: 0.88,
                SourceIntent: StructuredQueryIntent.Mechanics,
                SourceField: "KeyMechanics",
                SharedGameId: analysis.SharedGameId)
        ];
    }

    private static List<StructuredRetrievalResult> RetrieveGlossary(
        RulebookAnalysis analysis, string query, string? matchedTerm)
    {
        if (analysis.KeyConcepts.Count == 0)
            return [];

        var results = new List<StructuredRetrievalResult>();
        var lowerQuery = query.ToLowerInvariant();

        foreach (var concept in analysis.KeyConcepts)
        {
            var termLower = concept.Term.ToLowerInvariant();

            // Exact term match (from classifier) or query contains the term
            var isExactMatch = matchedTerm != null &&
                               termLower.Contains(matchedTerm.ToLowerInvariant(), StringComparison.Ordinal);
            var isQueryMatch = lowerQuery.Contains(termLower, StringComparison.Ordinal);

            if (isExactMatch || isQueryMatch)
            {
                var confidence = isExactMatch ? 0.95 : 0.80;
                results.Add(new StructuredRetrievalResult(
                    Content: $"{concept.Term} ({concept.Category}): {concept.Definition}",
                    Confidence: confidence,
                    SourceIntent: StructuredQueryIntent.Glossary,
                    SourceField: "KeyConcepts",
                    SharedGameId: analysis.SharedGameId));
            }
        }

        return results;
    }

    private static List<StructuredRetrievalResult> RetrieveFaqs(
        RulebookAnalysis analysis, string query)
    {
        if (analysis.GeneratedFaqs.Count == 0)
            return [];

        var results = new List<StructuredRetrievalResult>();
        var lowerQuery = query.ToLowerInvariant();

        foreach (var faq in analysis.GeneratedFaqs)
        {
            // Simple word overlap scoring between query and FAQ question
            var faqWords = faq.Question.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var queryWords = lowerQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var commonWords = faqWords.Intersect(queryWords, StringComparer.OrdinalIgnoreCase)
                .Where(w => w.Length > 3) // Skip short words
                .Count();

            var overlapRatio = faqWords.Length > 0 ? (double)commonWords / faqWords.Length : 0.0;

            if (overlapRatio >= 0.3)
            {
                var confidence = Math.Min(0.95, (double)faq.Confidence * 0.8 + overlapRatio * 0.3);
                results.Add(new StructuredRetrievalResult(
                    Content: $"Q: {faq.Question}\nA: {faq.Answer}\n(Source: {faq.SourceSection})",
                    Confidence: confidence,
                    SourceIntent: StructuredQueryIntent.Faq,
                    SourceField: "GeneratedFaqs",
                    SharedGameId: analysis.SharedGameId));
            }
        }

        return results;
    }
}
