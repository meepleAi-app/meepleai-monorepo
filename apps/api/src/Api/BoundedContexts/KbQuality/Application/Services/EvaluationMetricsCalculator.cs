namespace Api.BoundedContexts.KbQuality.Application.Services;

public sealed class EvaluationMetricsCalculator : IEvaluationMetricsCalculator
{
    public PrecisionAndRanking Compute(IReadOnlyList<QueryResult> queryResults)
    {
        ArgumentNullException.ThrowIfNull(queryResults);
        if (queryResults.Count == 0)
        {
            return new PrecisionAndRanking(0, 0, 0, 0);
        }

        double sumP1 = 0, sumP3 = 0, sumP5 = 0, sumRr = 0;

        foreach (var q in queryResults)
        {
            sumP1 += PrecisionAt(q.RelevantHits, 1);
            sumP3 += PrecisionAt(q.RelevantHits, 3);
            sumP5 += PrecisionAt(q.RelevantHits, 5);
            sumRr += ReciprocalRank(q.RelevantHits);
        }

        var n = queryResults.Count;
        return new PrecisionAndRanking(
            At1: sumP1 / n,
            At3: sumP3 / n,
            At5: sumP5 / n,
            Mrr: sumRr / n);
    }

    private static double PrecisionAt(IReadOnlyList<bool> hits, int k)
    {
        if (hits.Count == 0) return 0;
        var bound = Math.Min(k, hits.Count);
        var relevant = 0;
        for (var i = 0; i < bound; i++)
        {
            if (hits[i]) relevant++;
        }
        return (double)relevant / k;
    }

    private static double ReciprocalRank(IReadOnlyList<bool> hits)
    {
        for (var i = 0; i < hits.Count; i++)
        {
            if (hits[i]) return 1.0 / (i + 1);
        }
        return 0;
    }
}
