using Api.SharedKernel.Application.Interfaces;
using Api.Models;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;

/// <summary>
/// Handler for evaluating prompt versions with automated testing
/// Delegates to IPromptEvaluationService (infrastructure adapter for complex evaluation logic)
/// </summary>
public sealed class EvaluatePromptCommandHandler : ICommandHandler<EvaluatePromptCommand, PromptEvaluationResult>
{
    private readonly IPromptEvaluationService _evaluationService;
    private readonly ILogger<EvaluatePromptCommandHandler> _logger;

    public EvaluatePromptCommandHandler(
        IPromptEvaluationService evaluationService,
        ILogger<EvaluatePromptCommandHandler> logger)
    {
        _evaluationService = evaluationService;
        _logger = logger;
    }

    public async Task<PromptEvaluationResult> Handle(EvaluatePromptCommand command, CancellationToken cancellationToken)
    {
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
            cancellationToken);

        if (command.StoreResults)
        {
            await _evaluationService.StoreResultsAsync(result, cancellationToken);
            _logger.LogInformation(
                "Evaluation result {EvaluationId} stored to database",
                result.EvaluationId);
        }

        _logger.LogInformation(
            "Evaluation completed - Status: {Status}, Accuracy: {Accuracy:F1}%, Hallucination: {Hallucination:F1}%",
            result.Passed ? "PASSED" : "FAILED",
            result.Metrics.Accuracy,
            result.Metrics.HallucinationRate);

        return result;
    }
}
