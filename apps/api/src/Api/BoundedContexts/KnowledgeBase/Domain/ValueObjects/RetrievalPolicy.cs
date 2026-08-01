namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Explicit retrieval capability for a prompt-assembly call (#3389): the base <see cref="RetrievalProfile"/>
/// plus whether tier-derived RAG enhancements may be activated. Makes the in-session live path's retrieval
/// an intentional, observable choice instead of a silent side-effect of a null user tier.
/// </summary>
/// <param name="BaseProfile">Baseline retrieval depth before adaptive routing / debug overrides.</param>
/// <param name="EnhancementsEnabled">
/// When <c>false</c>, tier-derived enhancements (AdaptiveRouting/CRAG/RAPTOR/Fusion/Graph) are NOT activated,
/// even if a user tier is supplied. The live path uses <c>false</c> until a golden eval set exists (#3390).
/// </param>
internal sealed record RetrievalPolicy(RetrievalProfile BaseProfile, bool EnhancementsEnabled)
{
    /// <summary>Legacy behaviour: Default profile; enhancements gated only by the presence of a tier.</summary>
    public static RetrievalPolicy Default => new(RetrievalProfile.Default, EnhancementsEnabled: true);

    /// <summary>
    /// In-session live chat (#3389): explicit LiveSession profile with enhancements intentionally OFF.
    /// The live path is the hottest RAG path; enabling enhancements there needs a golden eval set (#3390)
    /// and CRAG web-fallback disabled on the closed rulebook domain — deferred, not silently dormant.
    /// </summary>
    public static RetrievalPolicy LiveSession => new(RetrievalProfile.LiveSession, EnhancementsEnabled: false);
}
