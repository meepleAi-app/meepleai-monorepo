using Api.BoundedContexts.KbQuality.Domain.Evaluation;

namespace Api.BoundedContexts.KbQuality.Application.Services;

public interface IQualityBandResolver
{
    QualityBand Resolve(EvaluationMetrics metrics);
}
