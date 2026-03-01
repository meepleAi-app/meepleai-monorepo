using Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;

/// <summary>
/// Initializes a new TurnOrder for a session with the given player list.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal sealed record InitializeTurnOrderCommand(
    Guid SessionId,
    IReadOnlyList<string> PlayerOrder) : ICommand<TurnOrderDto>;

/// <summary>
/// Advances the turn to the next player, wrapping and incrementing round as needed.
/// </summary>
internal sealed record AdvanceTurnCommand(Guid SessionId) : ICommand<TurnOrderDto>;

/// <summary>
/// Replaces the player order with a new ordered list.
/// </summary>
internal sealed record ReorderPlayersCommand(
    Guid SessionId,
    IReadOnlyList<string> NewPlayerOrder) : ICommand<TurnOrderDto>;

/// <summary>
/// Resets the turn order back to round 1, first player.
/// </summary>
internal sealed record ResetTurnOrderCommand(Guid SessionId) : ICommand<TurnOrderDto>;
