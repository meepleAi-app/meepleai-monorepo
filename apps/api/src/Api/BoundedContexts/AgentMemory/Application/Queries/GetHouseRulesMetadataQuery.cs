using Api.BoundedContexts.AgentMemory.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.AgentMemory.Application.Queries;

/// <summary>
/// Query to retrieve house-rules metadata counts for the SP6 §F drawer ConnectionBar.
/// Returns 4 counts (agent · game · session · kb) for the given game and owner pair.
/// </summary>
internal record GetHouseRulesMetadataQuery(Guid GameId, Guid OwnerId)
    : IQuery<HouseRulesMetadataDto>;
