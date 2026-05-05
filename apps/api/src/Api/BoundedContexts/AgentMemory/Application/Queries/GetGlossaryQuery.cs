using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to retrieve all glossary entries for a game's memory by game and owner IDs.
/// </summary>
internal record GetGlossaryQuery(Guid GameId, Guid OwnerId)
    : IQuery<IReadOnlyList<GlossaryEntryDto>>;
