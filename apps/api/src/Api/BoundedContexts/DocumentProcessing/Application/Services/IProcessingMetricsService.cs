using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Service for managing PDF processing metrics and ETA calculation.
/// Issue #4212: Historical metrics storage and statistical analysis.
/// </summary>
internal interface IProcessingMetricsService
{
    /// <summary>
    /// Records the duration of a processing step for historical analysis.
    /// </summary>
    /// <param name="pdfId">PDF document ID</param>
    /// <param name="step">Processing step completed</param>
    /// <param name="duration">Duration of the step</param>
    /// <param name="pdfSizeBytes">PDF file size in bytes</param>
    /// <param name="pageCount">Number of pages in the PDF</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RecordStepDurationAsync(
        Guid pdfId,
        PdfProcessingState step,
        TimeSpan duration,
        long pdfSizeBytes,
        int pageCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets average duration statistics for a processing step.
    /// Calculates from last 100 records for the step.
    /// </summary>
    /// <param name="step">Processing step to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistical metrics (avg, median, P95, sample size)</returns>
    Task<StepDurationStats> GetAverageDurationAsync(
        PdfProcessingState step,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates estimated time remaining for PDF processing.
    /// Uses historical averages adjusted for PDF size and page count.
    /// </summary>
    /// <param name="pdfId">PDF document ID</param>
    /// <param name="currentStep">Current processing step</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Estimated time remaining (null if insufficient data or completed)</returns>
    Task<TimeSpan?> CalculateETAAsync(
        Guid pdfId,
        PdfProcessingState currentStep,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated statistics for all processing steps.
    /// Used by admin dashboard endpoint.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of step statistics</returns>
    Task<Dictionary<string, StepDurationStats>> GetAllStepStatisticsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old metrics keeping only the most recent N records per step.
    /// Called by background maintenance job.
    /// </summary>
    /// <param name="retainPerStep">Number of records to retain per step</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records deleted</returns>
    Task<int> CleanupOldMetricsAsync(
        int retainPerStep = 1000,
        CancellationToken cancellationToken = default);
}
