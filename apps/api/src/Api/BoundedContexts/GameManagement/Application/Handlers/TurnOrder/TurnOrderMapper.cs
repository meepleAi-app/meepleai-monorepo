using Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.TurnOrder;

/// <summary>
/// Maps TurnOrder domain entities to DTOs.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal static class TurnOrderMapper
{
    internal static TurnOrderDto ToDto(Domain.Entities.TurnOrder.TurnOrder turnOrder)
    {
        return new TurnOrderDto(
            Id: turnOrder.Id,
            SessionId: turnOrder.SessionId,
            PlayerOrder: turnOrder.PlayerOrder,
            CurrentIndex: turnOrder.CurrentIndex,
            CurrentPlayer: turnOrder.CurrentPlayer,
            NextPlayer: turnOrder.NextPlayer,
            RoundNumber: turnOrder.RoundNumber,
            CreatedAt: turnOrder.CreatedAt,
            UpdatedAt: turnOrder.UpdatedAt);
    }
}
