using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>Neutral per-arm candidate — only what scoring needs. Adapters build the Key.</summary>
internal readonly record struct FusionCandidate(
    string Key,
    string Content,
    GameBookRole RoleTags,
    string? Heading,   // #3270: chunk heading-path label (nullable) for the heading-match boost
    int Rank,          // 1-based within THIS arm (cosine desc / ts_rank_cd desc)
    float SourceScore);

internal sealed record FusionOptions(
    float VectorWeight = 0.7f,
    float KeywordWeight = 0.3f,
    int RrfK = FusionSignals.DefaultRrfK,
    GameBookRole QueryRoleHint = GameBookRole.None,
    IReadOnlyList<string>? QueryTerms = null); // #3270: normalized query terms for the heading boost

/// <summary>Scoring result keyed by Key; adapters re-join their arm items for I/O-specific fields.</summary>
internal readonly record struct FusedCandidate(
    string Key,
    string Content,
    GameBookRole RoleTags,
    string? Heading,   // #3270: merged heading (prefers vector arm)
    float HybridScore,
    float? VectorScore,
    float? KeywordScore,
    int? VectorRank,
    int? KeywordRank,
    int Rank);         // 1-based rank in fused order

/// <summary>
/// The single canonical hybrid fusion (#3270): weighted RRF + legend-demotion + role-boost,
/// I/O-type agnostic. Pure — no logging, no injected state.
/// </summary>
internal static class HybridFusionCore
{
    internal static IReadOnlyList<FusedCandidate> Fuse(
        IReadOnlyList<FusionCandidate> vectorArm,
        IReadOnlyList<FusionCandidate> keywordArm,
        FusionOptions options)
    {
        // Dedup within each arm: keep the best (lowest) rank per Key. Total (never throws).
        var vec = BestPerKey(vectorArm);
        var kw = BestPerKey(keywordArm);

        var scored = new List<FusedCandidate>();
        foreach (var key in vec.Keys.Union(kw.Keys, StringComparer.Ordinal))
        {
            var hasV = vec.TryGetValue(key, out var v);
            var hasK = kw.TryGetValue(key, out var k);

            float vectorRrf = hasV ? options.VectorWeight / (options.RrfK + v.Rank) : 0f;
            float keywordRrf = hasK ? options.KeywordWeight / (options.RrfK + k.Rank) : 0f;
            float rrfSum = vectorRrf + keywordRrf;

            // Prefer vector arm content (load-bearing: legend factor is computed from it).
            string content = hasV ? v.Content : k.Content;
            GameBookRole roleTags = (hasV ? v.RoleTags : GameBookRole.None) | (hasK ? k.RoleTags : GameBookRole.None);
            // #3270: prefer vector-arm heading; fall back to keyword arm when vector had none.
            string? heading = (hasV ? v.Heading : null) ?? (hasK ? k.Heading : null);

            float legendFactor = FusionSignals.ComputeLegendPenaltyFactor(content);
            // #3338 WP1b: demote short digit-dominated table/number-fragment chunks that otherwise
            // out-rank real section prose. Multiplicative with the legend factor.
            float numberNoiseFactor = FusionSignals.ComputeNumberNoiseFactor(content);
            float roleBoost = FusionSignals.ComputeRoleMatchBoost(options.QueryRoleHint, roleTags);
            float headingBoost = FusionSignals.ComputeHeadingMatchBoost(options.QueryTerms, heading);
            float hybridScore = (rrfSum * (1f - legendFactor) * (1f - numberNoiseFactor)) + roleBoost + headingBoost;

            scored.Add(new FusedCandidate(
                Key: key,
                Content: content,
                RoleTags: roleTags,
                Heading: heading,
                HybridScore: hybridScore,
                VectorScore: hasV ? v.SourceScore : (float?)null,
                KeywordScore: hasK ? k.SourceScore : (float?)null,
                VectorRank: hasV ? v.Rank : (int?)null,
                KeywordRank: hasK ? k.Rank : (int?)null,
                Rank: 0)); // assigned after sort
        }

        // Order by hybridScore desc; deterministic tie-break by Key ordinal.
        var ordered = scored
            .OrderByDescending(c => c.HybridScore)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select((c, i) => c with { Rank = i + 1 })
            .ToList();

        return ordered;
    }

    private static Dictionary<string, FusionCandidate> BestPerKey(IReadOnlyList<FusionCandidate> arm)
    {
        var best = new Dictionary<string, FusionCandidate>(StringComparer.Ordinal);
        foreach (var c in arm)
        {
            if (!best.TryGetValue(c.Key, out var existing) || c.Rank < existing.Rank)
            {
                best[c.Key] = c;
            }
        }
        return best;
    }
}
