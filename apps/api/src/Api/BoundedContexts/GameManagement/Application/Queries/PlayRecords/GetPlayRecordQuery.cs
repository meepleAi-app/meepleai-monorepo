using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Query to retrieve full play record details.
/// Issue #3890: CQRS queries for play records.
/// </summary>
internal record GetPlayRecordQuery(Guid RecordId) : IQuery<PlayRecordDto>;
