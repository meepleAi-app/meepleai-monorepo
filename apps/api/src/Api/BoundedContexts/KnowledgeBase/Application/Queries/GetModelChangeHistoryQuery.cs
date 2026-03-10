using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Issue #5503: Returns model change history for admin UI audit trail.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
public sealed record GetModelChangeHistoryQuery(
    string? ModelId = null,
    int Limit = 50) : IQuery<GetModelChangeHistoryResult>;

public sealed record ModelChangeHistoryDto(
    Guid Id,
    string ModelId,
    string ChangeType,
    string? PreviousModelId,
    string? NewModelId,
    string? AffectedStrategy,
    string Reason,
    bool IsAutomatic,
    Guid? ChangedByUserId,
    DateTime OccurredAt);

public sealed record GetModelChangeHistoryResult(IReadOnlyList<ModelChangeHistoryDto> Changes);
