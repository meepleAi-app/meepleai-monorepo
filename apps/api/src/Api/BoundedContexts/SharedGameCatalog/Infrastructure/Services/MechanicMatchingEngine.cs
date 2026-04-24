using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Default implementation of <see cref="IMechanicMatchingEngine"/>. Pure CPU, stateless,
/// synchronous. Uses greedy first-match (no optimization / Hungarian algorithm): each
/// analysis claim is paired with the first still-unmatched golden claim meeting both
/// keyword-Jaccard and embedding-cosine thresholds.
/// </summary>
internal sealed class MechanicMatchingEngine : IMechanicMatchingEngine
{
    private const double JaccardThreshold = 0.50;
    private const double CosineThreshold = 0.75;

    public MatchResult Match(
        IReadOnlyList<AnalysisClaim> analysisClaims,
        IReadOnlyList<MechanicGoldenClaim> golden,
        IReadOnlyList<MechanicGoldenBggTag> bggTags,
        IReadOnlyList<AnalysisMechanicTag> analysisTags,
        CertificationThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(analysisClaims);
        ArgumentNullException.ThrowIfNull(golden);
        ArgumentNullException.ThrowIfNull(bggTags);
        ArgumentNullException.ThrowIfNull(analysisTags);
        ArgumentNullException.ThrowIfNull(thresholds);

        var matches = new List<MatchDetail>(capacity: Math.Min(analysisClaims.Count, golden.Count));
        var unmatched = new List<MechanicGoldenClaim>(golden);

        foreach (var analysis in analysisClaims)
        {
            for (var i = 0; i < unmatched.Count; i++)
            {
                var candidate = unmatched[i];
                if (!MeetsKeywordThreshold(analysis.Keywords, candidate.Keywords))
                {
                    continue;
                }
                if (!MeetsEmbeddingThreshold(analysis.Embedding, candidate.Embedding))
                {
                    continue;
                }

                var pageDiff = Math.Abs(analysis.Page - candidate.ExpectedPage);
                var pageAccurate = pageDiff <= thresholds.MaxPageTolerance;
                matches.Add(new MatchDetail(candidate.Id, analysis.Id, pageAccurate, pageDiff));
                unmatched.RemoveAt(i);
                break;
            }
        }

        var coveragePct = golden.Count == 0
            ? 0m
            : Math.Round((decimal)matches.Count / golden.Count * 100m, 2);

        var pageAccurateCount = matches.Count(m => m.PageAccurate);
        var pageAccuracyPct = matches.Count == 0
            ? 0m
            : Math.Round((decimal)pageAccurateCount / matches.Count * 100m, 2);

        var bggMatchPct = ComputeBggMatchPct(analysisTags, bggTags);

        return new MatchResult(coveragePct, pageAccuracyPct, bggMatchPct, matches);
    }

    private static bool MeetsKeywordThreshold(string[] a, string[] b)
        => Jaccard(a, b) >= JaccardThreshold;

    private static bool MeetsEmbeddingThreshold(float[]? a, float[]? b)
    {
        if (a is null || b is null)
        {
            return false;
        }
        return Cosine(a, b) >= CosineThreshold;
    }

    private static double Jaccard(string[] a, string[] b)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return 0;
        }

        var sa = new HashSet<string>(a, StringComparer.Ordinal);
        var sb = new HashSet<string>(b, StringComparer.Ordinal);
        var intersectionCount = 0;
        foreach (var item in sa)
        {
            if (sb.Contains(item))
            {
                intersectionCount++;
            }
        }
        var union = sa.Count + sb.Count - intersectionCount;
        return union == 0 ? 0 : (double)intersectionCount / union;
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0;
        }

        double dot = 0;
        double na = 0;
        double nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        return denom < 1e-9 ? 0 : dot / denom;
    }

    private static decimal ComputeBggMatchPct(
        IReadOnlyList<AnalysisMechanicTag> analysisTags,
        IReadOnlyList<MechanicGoldenBggTag> bggTags)
    {
        if (bggTags.Count == 0)
        {
            return 0m;
        }

        var analysisSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in analysisTags)
        {
            analysisSet.Add(tag.Name);
        }

        var matchCount = 0;
        foreach (var bgg in bggTags)
        {
            if (analysisSet.Contains(bgg.Name))
            {
                matchCount++;
            }
        }

        return Math.Round((decimal)matchCount / bggTags.Count * 100m, 2);
    }
}
