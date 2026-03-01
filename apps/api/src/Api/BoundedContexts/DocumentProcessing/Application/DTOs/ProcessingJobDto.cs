namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for a processing job in the queue list view.
/// Issue #4731: Queue queries.
/// </summary>
internal record ProcessingJobDto(
    Guid Id,
    Guid PdfDocumentId,
    string PdfFileName,
    Guid UserId,
    string Status,
    int Priority,
    string? CurrentStep,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    int RetryCount,
    int MaxRetries,
    bool CanRetry
);

/// <summary>
/// DTO for job detail view including steps and logs.
/// Issue #4731: Queue queries.
/// </summary>
internal record ProcessingJobDetailDto(
    Guid Id,
    Guid PdfDocumentId,
    string PdfFileName,
    Guid UserId,
    string Status,
    int Priority,
    string? CurrentStep,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    int RetryCount,
    int MaxRetries,
    bool CanRetry,
    IReadOnlyList<ProcessingStepDto> Steps
);

/// <summary>
/// DTO for a processing step.
/// Issue #4731: Queue queries.
/// </summary>
internal record ProcessingStepDto(
    Guid Id,
    string StepName,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    double? DurationMs,
    string? MetadataJson,
    IReadOnlyList<StepLogEntryDto> LogEntries
);

/// <summary>
/// DTO for a step log entry.
/// Issue #4731: Queue queries.
/// </summary>
internal record StepLogEntryDto(
    Guid Id,
    DateTimeOffset Timestamp,
    string Level,
    string Message
);

/// <summary>
/// Paginated response for queue listing.
/// Issue #4731: Queue queries.
/// </summary>
internal record PaginatedQueueResponse(
    IReadOnlyList<ProcessingJobDto> Jobs,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
