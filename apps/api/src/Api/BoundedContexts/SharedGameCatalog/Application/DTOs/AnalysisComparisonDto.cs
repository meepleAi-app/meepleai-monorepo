namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for analysis comparison result.
/// Issue #5461: Analysis comparison tool.
/// </summary>
internal sealed record AnalysisComparisonDto(
    Guid LeftId,
    Guid RightId,
    string LeftVersion,
    string RightVersion,
    DateTime LeftAnalyzedAt,
    DateTime RightAnalyzedAt,
    decimal ConfidenceScoreDelta,
    ListDiffDto<string> MechanicsDiff,
    ListDiffDto<string> CommonQuestionsDiff,
    ListDiffDto<string> KeyConceptsDiff,
    FaqDiffDto FaqDiff,
    bool SummaryChanged,
    string? LeftSummary,
    string? RightSummary
);

internal sealed record ListDiffDto<T>(
    List<T> Added,
    List<T> Removed,
    List<T> Unchanged
);

internal sealed record FaqDiffDto(
    List<FaqDiffItemDto> Added,
    List<FaqDiffItemDto> Removed,
    List<FaqModifiedDto> Modified,
    List<FaqDiffItemDto> Unchanged
);

internal sealed record FaqDiffItemDto(
    string Question,
    string Answer,
    decimal Confidence
);

internal sealed record FaqModifiedDto(
    string Question,
    string LeftAnswer,
    string RightAnswer,
    decimal LeftConfidence,
    decimal RightConfidence
);
