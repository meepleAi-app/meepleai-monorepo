using MediatR;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Handlers;

/// <summary>
/// Handler for RunEvaluationCommand.
/// Loads dataset and runs RAG evaluation.
/// </summary>
internal sealed class RunEvaluationCommandHandler : IRequestHandler<RunEvaluationCommand, EvaluationResult>
{
    private readonly IDatasetEvaluationService _evaluationService;
    private readonly ILogger<RunEvaluationCommandHandler> _logger;

    public RunEvaluationCommandHandler(
        IDatasetEvaluationService evaluationService,
        ILogger<RunEvaluationCommandHandler> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EvaluationResult> Handle(RunEvaluationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Starting evaluation run with dataset '{DatasetPath}', configuration '{Configuration}'",
            request.DatasetPath,
            request.Configuration);

        // Load dataset from file
        EvaluationDataset dataset;
        try
        {
            var json = await File.ReadAllTextAsync(request.DatasetPath, cancellationToken).ConfigureAwait(false);
            dataset = EvaluationDataset.FromJson(json);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result/Response pattern.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to load dataset from '{DatasetPath}'", request.DatasetPath);
            throw new InvalidOperationException($"Failed to load dataset: {ex.Message}", ex);
        }

        // Validate dataset
        var (isValid, errors) = dataset.Validate();
        if (!isValid)
        {
            _logger.LogWarning(
                "Dataset validation warnings: {Errors}",
                string.Join("; ", errors));
        }

        // Create evaluation options
        var options = new EvaluationOptions
        {
            Configuration = request.Configuration,
            TopK = request.TopK,
            EvaluateAnswerCorrectness = request.EvaluateAnswerCorrectness,
            MaxSamples = request.MaxSamples
        };

        // Run evaluation
        var result = await _evaluationService.EvaluateDatasetAsync(dataset, options, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Evaluation completed: {SuccessCount}/{TotalCount} samples, Recall@10={Recall:F2}",
            result.SuccessCount,
            result.SampleResults.Count,
            result.Metrics.RecallAt10);

        return result;
    }
}
