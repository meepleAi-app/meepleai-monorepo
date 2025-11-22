using Api.SharedKernel.Application.Interfaces;
using Api.Models;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Queries.PromptEvaluation;

/// <summary>
/// Handler for retrieving historical evaluation results for a template
/// Delegates to IPromptEvaluationService (infrastructure adapter)
/// </summary>
public sealed class GetEvaluationHistoryQueryHandler : IQueryHandler<GetEvaluationHistoryQuery, List<PromptEvaluationResult>>
{
    private readonly IPromptEvaluationService _evaluationService;
    private readonly ILogger<GetEvaluationHistoryQueryHandler> _logger;

    public GetEvaluationHistoryQueryHandler(
        IPromptEvaluationService evaluationService,
        ILogger<GetEvaluationHistoryQueryHandler> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<PromptEvaluationResult>> Handle(GetEvaluationHistoryQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving evaluation history for template {TemplateId}, limit {Limit}",
            query.TemplateId, query.Limit);

        // Delegate to infrastructure service for database retrieval and deserialization
        var results = await _evaluationService.GetHistoricalResultsAsync(
            query.TemplateId.ToString(),
            query.Limit,
            cancellationToken);

        _logger.LogDebug("Retrieved {Count} historical evaluation results", results.Count);

        return results;
    }
}
