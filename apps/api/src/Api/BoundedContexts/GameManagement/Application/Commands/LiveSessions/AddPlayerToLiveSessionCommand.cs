using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to add a player to a live game session.
/// Returns the new player's ID.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
/// <remarks>
/// #4398: <c>RequestedByUserId</c> è chi chiede dalla rotta HTTP, e deve essere il proprietario della sessione: un nome
/// di giocatore entra nel contesto dell'assistente del proprietario (#4393). Null per un chiamante interno, già
/// autorizzato (i comandi che creano la sessione, i test che la seminano).
/// </remarks>
internal record AddPlayerToLiveSessionCommand(
    Guid SessionId,
    string DisplayName,
    PlayerColor Color,
    Guid? UserId = null,
    PlayerRole? Role = null,
    string? AvatarUrl = null,
    Guid? RequestedByUserId = null
) : ICommand<Guid>;
