namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IEvaluationCostEstimator
{
    Task<decimal> EstimateAsync(Guid docId, CancellationToken ct);
}
