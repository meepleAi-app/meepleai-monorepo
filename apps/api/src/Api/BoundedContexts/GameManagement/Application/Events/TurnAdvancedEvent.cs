using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Events;

/// <summary>
/// SSE event broadcast when a turn is advanced.
/// Published via ISessionBroadcastService with event type "turn:advanced".
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal sealed class TurnAdvancedEvent : INotification
{
    public string CurrentPlayerName { get; init; }
    public string PreviousPlayerName { get; init; }
    public string NextPlayerName { get; init; }
    public int RoundNumber { get; init; }

    public TurnAdvancedEvent(
        string currentPlayerName,
        string previousPlayerName,
        string nextPlayerName,
        int roundNumber)
    {
        CurrentPlayerName = currentPlayerName;
        PreviousPlayerName = previousPlayerName;
        NextPlayerName = nextPlayerName;
        RoundNumber = roundNumber;
    }
}
