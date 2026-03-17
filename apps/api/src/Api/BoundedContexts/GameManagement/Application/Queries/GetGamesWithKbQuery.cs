using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all games in a user's library that have KB-linked rulebooks.
/// Powers the chat selection screen: games with "ready" KB are selectable, "processing" are disabled.
/// </summary>
internal sealed record GetGamesWithKbQuery(Guid UserId) : IQuery<IReadOnlyList<GameWithKbDto>>;
