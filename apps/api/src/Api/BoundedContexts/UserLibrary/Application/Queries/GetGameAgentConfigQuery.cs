using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get the custom AI agent configuration for a game in user's library.
/// Returns null if no custom configuration exists.
/// </summary>
internal record GetGameAgentConfigQuery(
    Guid UserId,
    Guid GameId
) : IQuery<AgentConfigDto?>;
