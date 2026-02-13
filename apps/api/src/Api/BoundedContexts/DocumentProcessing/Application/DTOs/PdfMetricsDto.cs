using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// PDF processing metrics response for performance tracking and ETA calculation.
/// Issue #4219: Duration metrics and ETA calculation for user expectation management.
/// </summary>
/// <param name="DocumentId">PDF document unique identifier</param>
/// <param name="CurrentState">Current processing state</param>
/// <param name="ProgressPercentage">Overall completion percentage (0-100)</param>
/// <param name="TotalDuration">Total time elapsed since upload (null if not completed)</param>
/// <param name="EstimatedTimeRemaining">Estimated time until completion (null if completed/failed)</param>
/// <param name="StateDurations">Duration spent in each processing state (key: state name, value: duration string)</param>
/// <param name="RetryCount">Number of retry attempts made</param>
/// <param name="PageCount">Total number of pages in the PDF</param>
internal record PdfMetricsDto(
    Guid DocumentId,
    PdfProcessingState CurrentState,
    int ProgressPercentage,
    TimeSpan? TotalDuration,
    TimeSpan? EstimatedTimeRemaining,
    Dictionary<string, TimeSpan> StateDurations,
    int RetryCount,
    int? PageCount
);
