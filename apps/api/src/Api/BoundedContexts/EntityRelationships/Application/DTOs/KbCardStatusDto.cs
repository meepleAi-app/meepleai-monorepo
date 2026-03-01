namespace Api.BoundedContexts.EntityRelationships.Application.DTOs;

/// <summary>
/// Status of a KB card (PDF document in the processing pipeline).
/// Populated in EntityLinkDto when TargetEntityType = KbCard (Issue #5188).
/// </summary>
public sealed record KbCardStatusDto(
    string FileName,
    long FileSizeBytes,
    string ProcessingState,
    int ProgressPercentage,
    bool CanRetry,
    string? ErrorCategory,
    string? ProcessingError
);
