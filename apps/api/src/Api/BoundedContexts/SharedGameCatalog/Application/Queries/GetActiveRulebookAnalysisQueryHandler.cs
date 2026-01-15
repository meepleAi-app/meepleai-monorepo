using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetActiveRulebookAnalysisQuery.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
internal sealed class GetActiveRulebookAnalysisQueryHandler
    : IQueryHandler<GetActiveRulebookAnalysisQuery, RulebookAnalysisDto?>
{
    private readonly IRulebookAnalysisRepository _analysisRepository;
    private readonly ILogger<GetActiveRulebookAnalysisQueryHandler> _logger;

    public GetActiveRulebookAnalysisQueryHandler(
        IRulebookAnalysisRepository analysisRepository,
        ILogger<GetActiveRulebookAnalysisQueryHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
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

        var analysis = await _analysisRepository.GetActiveAnalysisAsync(
            query.SharedGameId,
            query.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        if (analysis is null)
        {
            _logger.LogInformation(
                "No active rulebook analysis found for game {SharedGameId}, PDF {PdfDocumentId}",
                query.SharedGameId,
                query.PdfDocumentId);
            return null;
        }

        return MapToDto(analysis);
    }

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
