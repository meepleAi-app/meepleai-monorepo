namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// The values the playground saves in <see cref="RagExecution.Strategy"/>: the strategy a run was generated with.
/// They are not the retrieval strategies of the RAG dashboard options (<c>Hybrid</c>, <c>Semantic</c>, …): the
/// dashboard's per-strategy routes used to accept only those, so the ids that <c>/overview</c> returned for the saved
/// runs got a 404 (#4677). One definition, used by the writer (<c>ResolveStrategy</c>) and by the routes that read.
/// </summary>
public static class RagExecutionStrategies
{
    public const string SingleModel = "SingleModel";

    public const string RetrievalOnly = "RetrievalOnly";

    public const string MultiModelConsensus = "MultiModelConsensus";

    /// <summary>Every strategy the playground can save a run with.</summary>
    public static readonly IReadOnlyList<string> All = [SingleModel, RetrievalOnly, MultiModelConsensus];
}
