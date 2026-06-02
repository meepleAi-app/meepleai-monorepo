namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IEvaluationMetricsCalculator
{
    PrecisionAndRanking Compute(IReadOnlyList<QueryResult> queryResults);
}

public sealed record PrecisionAndRanking(double At1, double At3, double At5, double Mrr);
