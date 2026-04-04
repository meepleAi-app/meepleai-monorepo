using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all games that have at least one KB-linked rulebook (via EntityLink)
/// for a given user. Used by the chat selection screen to show which games are ready for AI chat.
/// </summary>
internal record GetGamesWithKbQuery(Guid UserId) : IQuery<IReadOnlyList<GameWithKbDto>>;
