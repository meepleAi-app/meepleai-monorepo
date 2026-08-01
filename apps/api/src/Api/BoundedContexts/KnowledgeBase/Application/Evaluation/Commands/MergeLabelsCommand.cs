using MediatR;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Command to merge a human-reviewed <see cref="LabelingReview"/> back into a dataset:
/// for each reviewed sample, <see cref="EvaluationSample.RelevantChunkIds"/> is set to the
/// candidate chunk ids marked <c>Relevant == true</c>.
/// </summary>
/// <param name="DatasetPath">Path to the dataset JSON file to load samples from.</param>
/// <param name="Review">The reviewed labeling candidates to merge.</param>
internal sealed record MergeLabelsCommand(string DatasetPath, LabelingReview Review)
    : IRequest<EvaluationDataset>;
