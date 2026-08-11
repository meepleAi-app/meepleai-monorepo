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

    /// <summary>
    /// #3390 Slice 4 Step 1: parses enhancement identifiers (feature-flag key
    /// <c>rag.enhancement.crag-evaluation</c>, its suffix <c>crag-evaluation</c>, or the enum name
    /// <c>CragEvaluation</c>) into a combined <see cref="RagEnhancement"/> set. Empty input → None
    /// (grounded baseline). Fail-loud on an unknown identifier — a typo in an eval request must not
    /// silently degrade to a different enhancement set (mirrors the eval's fail-loud policy parsing).
    /// </summary>
    public static RagEnhancement ParseFlags(IEnumerable<string> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        const string prefix = "rag.enhancement.";
        var result = RagEnhancement.None;
        foreach (var raw in identifiers)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var id = raw.Trim();
            RagEnhancement? matched = null;
            foreach (var flag in AllFlags)
            {
                var key = flag.ToFeatureFlagKey();
                var suffix = key[prefix.Length..];
                if (key.Equals(id, StringComparison.OrdinalIgnoreCase)
                    || suffix.Equals(id, StringComparison.OrdinalIgnoreCase)
                    || flag.ToString().Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    matched = flag;
                    break;
                }
            }
            if (matched is null)
            {
                var valid = string.Join(", ", AllFlags.Select(f => f.ToFeatureFlagKey()[prefix.Length..]));
                throw new ArgumentException(
                    $"Unknown RAG enhancement '{raw}'. Valid values: {valid}.", nameof(identifiers));
            }
            result |= matched.Value;
        }
        return result;
    }
}
