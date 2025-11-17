using Api.BoundedContexts.Administration.Application.Abstractions;
using Api.Models;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;

/// <summary>
/// Handler for A/B comparison of prompt versions
/// Delegates to IPromptEvaluationService (infrastructure adapter for complex comparison logic)
/// </summary>
public sealed class ComparePromptVersionsCommandHandler : ICommandHandler<ComparePromptVersionsCommand, PromptComparisonResult>
{
    private readonly IPromptEvaluationService _evaluationService;
    private readonly ILogger<ComparePromptVersionsCommandHandler> _logger;

    public ComparePromptVersionsCommandHandler(
        IPromptEvaluationService evaluationService,
        ILogger<ComparePromptVersionsCommandHandler> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    public async Task<PromptComparisonResult> Handle(ComparePromptVersionsCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Comparing prompt versions for template {TemplateId}: Baseline {BaselineId} vs Candidate {CandidateId}",
            command.TemplateId, command.BaselineVersionId, command.CandidateVersionId);

        // Delegate to infrastructure service for:
        // - Evaluating both versions with the same dataset
        // - Calculating metric deltas
        // - Generating recommendation based on business rules (regression thresholds, improvement thresholds)
        var comparison = await _evaluationService.CompareVersionsAsync(
            command.TemplateId,
            command.BaselineVersionId,
            command.CandidateVersionId,
            command.DatasetPath,
            cancellationToken);

        _logger.LogInformation(
            "Comparison completed - Recommendation: {Recommendation}, Reason: {Reason}",
            comparison.Recommendation,
            comparison.RecommendationReason);

        return comparison;
    }
}
