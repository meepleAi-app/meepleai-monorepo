using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get aggregated statistics for AI requests.
/// </summary>
public record GetAiRequestStatsQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? UserId = null,
    string? GameId = null
) : IQuery<AiRequestStats>;
