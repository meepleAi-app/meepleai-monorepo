using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get game session state by session ID.
/// Issue #2403: GameSessionState Entity
/// </summary>
/// <param name="GameSessionId">ID of the game session</param>
public sealed record GetGameStateQuery(Guid GameSessionId) : IQuery<GameSessionStateDto?>;
