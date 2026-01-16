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

        // Cache DTO (non-nullable) using GetOrCreateAsync
        // Factory returns null-forgiving operator for cases where analysis doesn't exist
        RulebookAnalysisDto? cachedDto;
        try
        {
            cachedDto = await _cache.GetOrCreateAsync(
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

                        // Return a sentinel DTO to satisfy non-nullable constraint
                        // We'll check for this and return null to caller
                        return new RulebookAnalysisDto(
                            Guid.Empty,
                            Guid.Empty,
                            Guid.Empty,
                            string.Empty,
                            string.Empty,
                            new List<string>(),
                            null,
                            new List<ResourceDto>(),
                            new List<GamePhaseDto>(),
                            new List<string>(),
                            0m,
                            "0.0",
                            false,
                            Domain.ValueObjects.GenerationSource.AI,
                            DateTime.MinValue,
                            Guid.Empty);
                    }

                    return MapToDto(analysis);
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

        // Check for sentinel value (no analysis exists)
        if (cachedDto.Id == Guid.Empty)
        {
            return null;
        }

        return cachedDto;
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

    private static RulebookAnalysisDto MapToDto(Domain.Entities.RulebookAnalysis analysis)
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
            analysis.CreatedBy);
    }
}
