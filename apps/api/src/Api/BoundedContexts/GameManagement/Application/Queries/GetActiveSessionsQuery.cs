using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get all active game sessions (Setup, InProgress, Paused).
/// Issue #2755: Returns paginated response to match frontend schema.
/// </summary>
internal record GetActiveSessionsQuery(
    int? Limit = null,
    int? Offset = null
) : IQuery<PaginatedSessionsResponseDto>;
