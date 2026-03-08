namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Alert types for processing queue monitoring.
/// Issue #5460: Proactive alerts for stuck docs, high failure, queue depth.
/// </summary>
internal enum QueueAlertType
{
    DocumentStuck,
    QueueDepthHigh,
    HighFailureRate
}

/// <summary>
/// Alert severity levels.
/// </summary>
internal enum QueueAlertSeverity
{
    Warning,
    Critical
}

/// <summary>
/// DTO for a queue monitoring alert.
/// </summary>
internal sealed record QueueAlertDto(
    QueueAlertType Type,
    QueueAlertSeverity Severity,
    string Message,
    DateTimeOffset DetectedAt,
    object? Data);

/// <summary>
/// Data for stuck document alerts.
/// </summary>
internal sealed record StuckDocumentAlertData(
    Guid JobId,
    Guid PdfDocumentId,
    string? FileName,
    double StuckMinutes);

/// <summary>
/// Data for queue depth alerts.
/// </summary>
internal sealed record QueueDepthAlertData(
    int CurrentDepth,
    int Threshold);

/// <summary>
/// Data for high failure rate alerts.
/// </summary>
internal sealed record HighFailureRateAlertData(
    double FailureRatePercent,
    double Threshold,
    int FailedCount,
    int TotalCount);
