using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to retrieve all player memory records for a specific user.
/// </summary>
internal record GetPlayerStatsQuery(Guid UserId) : IQuery<List<PlayerMemoryDto>>;
