namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

[Flags]
public enum RagEnhancement
{
    None = 0,
    AdaptiveRouting = 1,
    CragEvaluation = 2,
    RaptorRetrieval = 4,
    RagFusionQueries = 8,
    GraphTraversal = 16
}

public static class RagEnhancementExtensions
{
    private static readonly RagEnhancement[] AllFlags =
    [
        RagEnhancement.AdaptiveRouting,
        RagEnhancement.CragEvaluation,
        RagEnhancement.RaptorRetrieval,
        RagEnhancement.RagFusionQueries,
        RagEnhancement.GraphTraversal
    ];

    public static string ToFeatureFlagKey(this RagEnhancement flag) => flag switch
    {
        RagEnhancement.AdaptiveRouting => "rag.enhancement.adaptive-routing",
        RagEnhancement.CragEvaluation => "rag.enhancement.crag-evaluation",
        RagEnhancement.RaptorRetrieval => "rag.enhancement.raptor-retrieval",
        RagEnhancement.RagFusionQueries => "rag.enhancement.rag-fusion-queries",
        RagEnhancement.GraphTraversal => "rag.enhancement.graph-traversal",
        _ => throw new ArgumentOutOfRangeException(nameof(flag), flag, "No feature flag key for this enhancement")
    };

    public static int GetExtraCredits(this RagEnhancement flag, bool useBalancedForAux)
    {
        if (!useBalancedForAux)
            return 0;

        return flag switch
        {
            RagEnhancement.CragEvaluation => 40,
            RagEnhancement.RagFusionQueries => 60,
            _ => 0
        };
    }

    public static IEnumerable<RagEnhancement> GetIndividualFlags(this RagEnhancement flags)
    {
        foreach (var flag in AllFlags)
        {
            if (flags.HasFlag(flag))
                yield return flag;
        }
    }
}
