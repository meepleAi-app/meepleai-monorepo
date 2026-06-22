namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

public enum EvaluationStatus
{
    Pending,
    GoldsetGenerating,
    Running,
    Completed,
    Failed,
    RateLimited,
    CostCapped,
}
