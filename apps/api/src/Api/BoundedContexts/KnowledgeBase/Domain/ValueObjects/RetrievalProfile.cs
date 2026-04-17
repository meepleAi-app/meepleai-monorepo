namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Immutable retrieval parameters resolved from query complexity and user tier.
/// Replaces hardcoded RAG search constants for adaptive retrieval depth.
/// </summary>
internal sealed record RetrievalProfile(
    int TopK,
    float MinScore,
    int FtsTopK,
    int WindowRadius)
{
    /// <summary>Baseline profile matching the original hardcoded constants.</summary>
    public static RetrievalProfile Default => new(TopK: 5, MinScore: 0.55f, FtsTopK: 10, WindowRadius: 1);
}
