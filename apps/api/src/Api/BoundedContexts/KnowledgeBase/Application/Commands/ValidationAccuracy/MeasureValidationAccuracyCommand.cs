using Api.Models;
using Api.SharedKernel.Application.Abstractions;
using ErrorOr;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.ValidationAccuracy;

/// <summary>
/// Command to measure validation accuracy baseline from evaluation results.
/// BGAI-039: Accuracy baseline measurement (target 80%+)
/// </summary>
public record MeasureValidationAccuracyCommand : ICommand<ErrorOr<ValidationAccuracyBaselineDto>>
{
    /// <summary>
    /// Context/description of what is being measured (e.g., "Overall Validation", "Layer 1: Confidence")
    /// </summary>
    public required string Context { get; init; }

    /// <summary>
    /// Dataset ID used for baseline measurement
    /// </summary>
    public required string DatasetId { get; init; }

    /// <summary>
    /// Evaluation result ID to calculate accuracy from
    /// </summary>
    public required Guid EvaluationId { get; init; }

    /// <summary>
    /// Expected number of valid responses (ground truth for accuracy calculation)
    /// </summary>
    public required int ExpectedValidCount { get; init; }

    /// <summary>
    /// Whether to store the baseline measurement in database
    /// </summary>
    public bool StoreBaseline { get; init; } = true;
}
