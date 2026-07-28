namespace Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;

/// <summary>
/// Response DTO for TurnOrder state.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal sealed record TurnOrderDto(
    Guid Id,
    Guid SessionId,
    IReadOnlyList<string> PlayerOrder,
    int CurrentIndex,
    string CurrentPlayer,
    string NextPlayer,
    int RoundNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);
