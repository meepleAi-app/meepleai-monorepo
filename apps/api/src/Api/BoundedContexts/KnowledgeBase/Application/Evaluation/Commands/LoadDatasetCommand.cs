using MediatR;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Command to load an evaluation dataset from file.
/// </summary>
internal sealed record LoadDatasetCommand : IRequest<EvaluationDataset>
{
    /// <summary>
    /// Path to the dataset JSON file.
    /// </summary>
    public required string FilePath { get; init; }
}
