using Api.SharedKernel.Application.Interfaces;
using Api.Models;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;

/// <summary>
/// Handler for A/B comparison of prompt versions
/// Delegates to IPromptEvaluationService (infrastructure adapter for complex comparison logic)
/// </summary>
internal sealed class ComparePromptVersionsCommandHandler : ICommandHandler<ComparePromptVersionsCommand, PromptComparisonResult>
{
    private readonly IPromptEvaluationService _evaluationService;
    private readonly ILogger<ComparePromptVersionsCommandHandler> _logger;

    public ComparePromptVersionsCommandHandler(
        IPromptEvaluationService evaluationService,
        ILogger<ComparePromptVersionsCommandHandler> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PromptComparisonResult> Handle(ComparePromptVersionsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
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
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Comparison completed - Recommendation: {Recommendation}, Reason: {Reason}",
            comparison.Recommendation,
            comparison.RecommendationReason);

        return comparison;
    }
}
