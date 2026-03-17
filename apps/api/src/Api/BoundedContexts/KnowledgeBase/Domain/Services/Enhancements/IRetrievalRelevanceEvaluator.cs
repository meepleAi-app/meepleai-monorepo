using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal interface IRetrievalRelevanceEvaluator
{
    Task<RelevanceEvaluation> EvaluateAsync(
        string query, IReadOnlyList<ScoredChunk> chunks, CancellationToken ct = default);
}
