using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;

/// <summary>
/// DTO for play record summary in list views.
/// Issue #3890: CQRS queries for play records.
/// Issue #1663: Phase 1 – GameId, WinnerPlayerIds and OutcomeType computed on read.
/// </summary>
public record PlayRecordSummaryDto(
    Guid Id,
    string GameName,
    DateTime SessionDate,
    TimeSpan? Duration,
    PlayRecordStatus Status,
    int PlayerCount,
    Guid? GameId,
    IReadOnlyList<Guid> WinnerPlayerIds,
    string OutcomeType
);
