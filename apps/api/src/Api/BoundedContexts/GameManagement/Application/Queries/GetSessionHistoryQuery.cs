using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get session history (Completed and Abandoned sessions) with filters.
/// </summary>
public record GetSessionHistoryQuery(
    Guid? GameId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? Limit = null,
    int? Offset = null
) : IQuery<List<GameSessionDto>>;
