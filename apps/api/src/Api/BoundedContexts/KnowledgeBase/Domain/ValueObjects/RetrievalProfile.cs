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
    private static readonly RetrievalProfile _default = new(TopK: 5, MinScore: 0.55f, FtsTopK: 10, WindowRadius: 1);

    /// <summary>Baseline profile matching the original hardcoded constants.</summary>
    public static RetrievalProfile Default => _default;

    private static readonly RetrievalProfile _liveSession = new(TopK: 5, MinScore: 0.55f, FtsTopK: 10, WindowRadius: 1);

    /// <summary>
    /// Explicit retrieval profile for the in-session live chat path (#3389). Decoupled from user tier:
    /// a null tier must NOT silently fall back to <see cref="Default"/>. Currently mirrors Default's depth,
    /// but is a NAMED, intentional choice so the live path's retrieval is observable and any future
    /// calibration is centralized here rather than being a side-effect of an absent tier. Enhancement
    /// wiring for the live path is deferred to #3390 (requires a golden eval set + CRAG web-fallback off).
    /// </summary>
    public static RetrievalProfile LiveSession => _liveSession;
}
