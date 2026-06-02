using Api.BoundedContexts.KbQuality.Application.Ports;
using Api.BoundedContexts.KbQuality.Domain.Evaluation;
using Api.BoundedContexts.KbQuality.Domain.Goldset;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IEvaluationExecutor
{
    Task<EvaluationOutcome> ExecuteAsync(
        Guid docId,
        PdfDocSnapshot pdf,
        IReadOnlyList<GoldsetQaPair> goldset,
        long seed,
        CancellationToken cancellationToken);
}

public sealed record EvaluationOutcome(EvaluationMetrics Metrics, decimal AdditionalCostUsd);
