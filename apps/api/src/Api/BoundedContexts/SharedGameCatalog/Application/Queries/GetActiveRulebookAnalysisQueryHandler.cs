using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetActiveRulebookAnalysisQuery.
/// Issue #2402: Rulebook Analysis Service
/// Issue #2453: Redis Caching Layer
/// </summary>
internal sealed class GetActiveRulebookAnalysisQueryHandler
    : IQueryHandler<GetActiveRulebookAnalysisQuery, RulebookAnalysisDto?>
{
    /// <summary>
    /// Wrapper for caching analysis query results (including null/not-found cases).
    /// Prevents cache stampede on "analysis doesn't exist" scenarios.
    /// </summary>
    private sealed record CachedRulebookAnalysisResult(RulebookAnalysisDto? Analysis);

    private readonly IRulebookAnalysisRepository _analysisRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetActiveRulebookAnalysisQueryHandler> _logger;

    public GetActiveRulebookAnalysisQueryHandler(
        IRulebookAnalysisRepository analysisRepository,
        IHybridCacheService cache,
        ILogger<GetActiveRulebookAnalysisQueryHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RulebookAnalysisDto?> Handle(
        GetActiveRulebookAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting active rulebook analysis for game {SharedGameId}, PDF {PdfDocumentId}",
            query.SharedGameId,
            query.PdfDocumentId);

        var cacheKey = GenerateCacheKey(query.SharedGameId, query.PdfDocumentId);
        var cacheTags = GenerateCacheTags(query.SharedGameId, query.PdfDocumentId);

        // Use wrapper record to cache both "found" and "not found" results
        // This prevents cache stampede on repeated queries for non-existent analyses
        CachedRulebookAnalysisResult cachedResult;
        try
        {
            cachedResult = await _cache.GetOrCreateAsync(
                cacheKey,
                async (ct) =>
                {
                    var analysis = await _analysisRepository.GetActiveAnalysisAsync(
                        query.SharedGameId,
                        query.PdfDocumentId,
                        ct).ConfigureAwait(false);

                    if (analysis is null)
                    {
                        _logger.LogInformation(
                            "No active rulebook analysis found for game {SharedGameId}, PDF {PdfDocumentId}",
                            query.SharedGameId,
                            query.PdfDocumentId);

                        return new CachedRulebookAnalysisResult(null);
                    }

                    return new CachedRulebookAnalysisResult(MapToDto(analysis));
                },
                cacheTags,
                TimeSpan.FromHours(24),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cache operation failed for game {SharedGameId}, PDF {PdfDocumentId}. Falling back to direct DB query.",
                query.SharedGameId,
                query.PdfDocumentId);

            // Fallback to direct DB query if cache fails
            var analysis = await _analysisRepository.GetActiveAnalysisAsync(
                query.SharedGameId,
                query.PdfDocumentId,
                cancellationToken).ConfigureAwait(false);

            return analysis is not null ? MapToDto(analysis) : null;
        }

        return cachedResult.Analysis;
    }

    private static string GenerateCacheKey(Guid sharedGameId, Guid pdfDocumentId)
        => $"meepleai:analysis:rulebook:{sharedGameId}:{pdfDocumentId}:active";

    private static string[] GenerateCacheTags(Guid sharedGameId, Guid pdfDocumentId)
        => new[]
        {
            $"game:{sharedGameId}",
            $"pdf:{pdfDocumentId}",
            "rulebook-analysis"
        };

    internal static RulebookAnalysisDto MapToDto(Domain.Entities.RulebookAnalysis analysis)
    {
        var victoryConditionsDto = analysis.VictoryConditions is not null
            ? new VictoryConditionsDto(
                analysis.VictoryConditions.Primary,
                analysis.VictoryConditions.Alternatives.ToList(),
                analysis.VictoryConditions.IsPointBased,
                analysis.VictoryConditions.TargetPoints)
            : null;

        var resourceDtos = analysis.Resources
            .Select(r => new ResourceDto(r.Name, r.Type, r.Usage, r.IsLimited))
            .ToList();

        var phaseDtos = analysis.GamePhases
            .Select(p => new GamePhaseDto(p.Name, p.Description, p.Order, p.IsOptional))
            .ToList();

        var keyConceptDtos = analysis.KeyConcepts
            .Select(kc => new KeyConceptDto(kc.Term, kc.Definition, kc.Category))
            .ToList();

        var generatedFaqDtos = analysis.GeneratedFaqs
            .Select(f => new GeneratedFaqDto(f.Question, f.Answer, f.SourceSection, f.Confidence, f.Tags))
            .ToList();

        return new RulebookAnalysisDto(
            analysis.Id,
            analysis.SharedGameId,
            analysis.PdfDocumentId,
            analysis.GameTitle,
            analysis.Summary,
            analysis.KeyMechanics.ToList(),
            victoryConditionsDto,
            resourceDtos,
            phaseDtos,
            analysis.CommonQuestions.ToList(),
            analysis.ConfidenceScore,
            analysis.Version,
            analysis.IsActive,
            analysis.Source,
            analysis.AnalyzedAt,
            analysis.CreatedBy,
            keyConceptDtos,
            generatedFaqDtos,
            analysis.GameStateSchemaJson,
            analysis.CompletionStatus.ToString(),
            analysis.MissingSections.ToList());
    }
}
