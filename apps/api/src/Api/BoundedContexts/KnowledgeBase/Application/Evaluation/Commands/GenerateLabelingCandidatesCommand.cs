using MediatR;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Command to generate labeling candidates for every sample in a dataset by querying
/// the live RAG pipeline and proposing the top-N retrieved chunks for human review.
/// </summary>
/// <param name="DatasetPath">Path to the dataset JSON file to load samples from.</param>
/// <param name="TopN">Number of top retrieved chunks to propose as candidates per sample.</param>
internal sealed record GenerateLabelingCandidatesCommand(string DatasetPath, int TopN = 10)
    : IRequest<LabelingReview>;
