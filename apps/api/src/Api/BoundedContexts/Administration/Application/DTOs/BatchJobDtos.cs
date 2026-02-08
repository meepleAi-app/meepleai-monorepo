namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for batch job details (Issue #3693)
/// </summary>
public sealed record BatchJobDto(
    Guid Id,
    string Type,
    string Status,
    int Progress,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DurationSeconds,
    string? ResultSummary,
    string? ErrorMessage,
    DateTime CreatedAt);

/// <summary>
/// DTO for batch job list with pagination (Issue #3693)
/// </summary>
public sealed record BatchJobListDto(
    List<BatchJobDto> Jobs,
    int Total);

/// <summary>
/// Request DTO for creating a batch job (Issue #3693)
/// </summary>
public sealed record CreateBatchJobRequest(
    string Type,
    string Parameters);

/// <summary>
/// Response DTO for batch job creation (Issue #3693)
/// </summary>
public sealed record CreateBatchJobResponse(
    Guid JobId);
