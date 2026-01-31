using Api.SharedKernel.Application.Interfaces;
using Api.Models;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;

/// <summary>
/// Handler for evaluating prompt versions with automated testing
/// Delegates to IPromptEvaluationService (infrastructure adapter for complex evaluation logic)
/// </summary>
internal sealed class EvaluatePromptCommandHandler : ICommandHandler<EvaluatePromptCommand, PromptEvaluationResult>
{
    private readonly IPromptEvaluationService _evaluationService;
    private readonly ILogger<EvaluatePromptCommandHandler> _logger;

    public EvaluatePromptCommandHandler(
        IPromptEvaluationService evaluationService,
        ILogger<EvaluatePromptCommandHandler> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PromptEvaluationResult> Handle(EvaluatePromptCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _logger.LogInformation(
            "Evaluating prompt template {TemplateId}, version {VersionId} with dataset {DatasetPath}",
            command.TemplateId, command.VersionId, command.DatasetPath);

        // Delegate to infrastructure service for:
        // - Dataset loading and validation (file I/O, security checks)
        // - Test case execution (RAG orchestration)
        // - Metric calculation (accuracy, hallucination, confidence, citations, latency)
        // - Pass/fail determination
        var result = await _evaluationService.EvaluateAsync(
            command.TemplateId,
            command.VersionId,
            command.DatasetPath,
            progressCallback: null,
            cancellationToken).ConfigureAwait(false);

        if (command.StoreResults)
        {
            await _evaluationService.StoreResultsAsync(result, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Evaluation result {EvaluationId} stored to database",
                result.EvaluationId);
        }

        _logger.LogInformation(
            "Evaluation completed - Status: {Status}, Accuracy: {Accuracy:F1}%, Relevance: {Relevance:F1}%",
            result.Passed ? "PASSED" : "FAILED",
            result.Metrics.Accuracy,
            result.Metrics.Relevance);

        return result;
    }
}
