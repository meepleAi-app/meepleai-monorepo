using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get paginated AI request logs with optional filters.
/// </summary>
public record GetAiRequestsQuery(
    int Limit = 100,
    int Offset = 0,
    string? Endpoint = null,
    string? UserId = null,
    string? GameId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IQuery<AiRequestListResult>;
