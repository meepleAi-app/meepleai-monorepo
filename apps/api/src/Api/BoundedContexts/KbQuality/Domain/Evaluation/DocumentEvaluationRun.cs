namespace Api.BoundedContexts.KbQuality.Domain.Evaluation;

/// <summary>
/// Per-document quality evaluation run (#1675).
/// </summary>
/// <remarks>
/// Task 2 stub: settable properties to satisfy the EF migration. Task 3 will
/// replace this with the proper aggregate (private setters + factory) and may
/// substitute <see cref="MetricsJson"/> with a strongly-typed owned record.
/// </remarks>
public sealed class DocumentEvaluationRun
{
    public Guid Id { get; set; }
    public Guid PdfDocumentId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string GoldsetVersion { get; set; } = "auto-v1";
    public long GoldsetGenerationSeed { get; set; }
    public string? MetricsJson { get; set; }
    public decimal? CostUsd { get; set; }
    public Guid TriggeredByAdminId { get; set; }
    public string? ErrorMessage { get; set; }
}
