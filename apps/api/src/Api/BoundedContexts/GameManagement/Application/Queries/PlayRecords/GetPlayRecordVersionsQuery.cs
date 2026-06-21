using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Query to retrieve the version history of a play record (creator-only, last 5 DESC).
/// Issue #2437-3: version history + restore.
/// </summary>
internal record GetPlayRecordVersionsQuery(Guid RecordId, Guid UserId)
    : IQuery<IReadOnlyList<PlayRecordVersionDto>>;
