using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// Resolves retrieval parameters from query complexity and user tier.
/// Pure static function — no dependencies, no state, fully testable.
/// </summary>
internal static class RetrievalProfileResolver
{
    public static RetrievalProfile Resolve(QueryComplexity complexity, LlmUserTier? tier)
    {
        var effectiveTier = tier ?? LlmUserTier.Anonymous;

        if (complexity.Level == QueryComplexityLevel.Simple)
            return RetrievalProfile.Default;

        if (effectiveTier == LlmUserTier.Anonymous)
            return RetrievalProfile.Default;

        var tierBucket = effectiveTier switch
        {
            LlmUserTier.User => TierBucket.Standard,
            _ => TierBucket.Elevated
        };

        return (complexity.Level, tierBucket) switch
        {
            (QueryComplexityLevel.Moderate, TierBucket.Standard) =>
                new RetrievalProfile(TopK: 8, MinScore: 0.50f, FtsTopK: 15, WindowRadius: 1),

            (QueryComplexityLevel.Moderate, TierBucket.Elevated) =>
                new RetrievalProfile(TopK: 10, MinScore: 0.45f, FtsTopK: 20, WindowRadius: 1),

            (QueryComplexityLevel.Complex, TierBucket.Standard) =>
                new RetrievalProfile(TopK: 10, MinScore: 0.45f, FtsTopK: 20, WindowRadius: 1),

            (QueryComplexityLevel.Complex, TierBucket.Elevated) =>
                new RetrievalProfile(TopK: 15, MinScore: 0.40f, FtsTopK: 25, WindowRadius: 2),

            _ => RetrievalProfile.Default
        };
    }

    private enum TierBucket { Standard, Elevated }
}
