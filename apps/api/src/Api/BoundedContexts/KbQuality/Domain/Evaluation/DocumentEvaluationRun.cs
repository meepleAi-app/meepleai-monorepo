namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

/// <summary>
/// Aggregate root tracking a single per-doc evaluation run lifecycle.
/// Issue #1675 — design doc §3.3.
/// </summary>
public sealed class DocumentEvaluationRun
{
    public Guid Id { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public EvaluationStatus Status { get; private set; }
    public string GoldsetVersion { get; private set; } = default!;
    public long GoldsetGenerationSeed { get; private set; }
    public EvaluationMetrics? Metrics { get; private set; }
    public decimal? CostUsd { get; private set; }
    public Guid TriggeredByAdminId { get; private set; }
    public string? ErrorMessage { get; private set; }

    // EF Core ctor
    private DocumentEvaluationRun() { }

    public static DocumentEvaluationRun Create(
        Guid pdfDocumentId,
        string goldsetVersion,
        Guid triggeredByAdminId,
        long? reuseSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goldsetVersion);
        if (pdfDocumentId == Guid.Empty) throw new ArgumentException("pdfDocumentId required", nameof(pdfDocumentId));
        if (triggeredByAdminId == Guid.Empty) throw new ArgumentException("triggeredByAdminId required", nameof(triggeredByAdminId));

        return new DocumentEvaluationRun
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfDocumentId,
            GoldsetVersion = goldsetVersion,
            GoldsetGenerationSeed = reuseSeed ?? Random.Shared.NextInt64(),
            TriggeredByAdminId = triggeredByAdminId,
            StartedAt = DateTime.UtcNow,
            Status = EvaluationStatus.Pending,
        };
    }

    public void TransitionTo(EvaluationStatus next)
    {
        if (Status is EvaluationStatus.Completed or EvaluationStatus.Failed
                   or EvaluationStatus.RateLimited or EvaluationStatus.CostCapped)
        {
            throw new InvalidOperationException($"Cannot transition from terminal state {Status} to {next}");
        }

        Status = next;
    }

    public void MarkCompleted(EvaluationMetrics metrics, decimal costUsd)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        Status = EvaluationStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Metrics = metrics;
        CostUsd = costUsd;
    }

    public void MarkFailed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        Status = EvaluationStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }
}
