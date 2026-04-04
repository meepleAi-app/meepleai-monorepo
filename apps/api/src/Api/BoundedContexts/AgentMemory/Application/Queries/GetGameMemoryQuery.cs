using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to retrieve a game's memory by game and owner IDs.
/// </summary>
internal record GetGameMemoryQuery(Guid GameId, Guid OwnerId) : IQuery<GameMemoryDto?>;
