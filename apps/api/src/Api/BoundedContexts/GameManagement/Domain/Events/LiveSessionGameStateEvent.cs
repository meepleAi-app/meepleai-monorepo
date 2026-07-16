using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Raised when a live game session's free-form game-state is updated (#3025 L1).
/// Forwarded to the SSE stream as "session:game-state".
///
/// <see cref="RawStateJson"/> is the opaque state serialized to a string, copied at raise
/// time (via <c>JsonDocument.RootElement.GetRawText()</c>) so the event does NOT hold the
/// aggregate's disposable <see cref="System.Text.Json.JsonDocument"/> — which is disposed on
/// the next <c>UpdateGameState</c> call and is not itself serializable by System.Text.Json.
///
/// NB: fires on EVERY <c>UpdateGameState</c> call — including snapshot-restore — which is
/// intentional (streaming a restored state to live clients is transparent).
/// </summary>
internal sealed class LiveSessionGameStateEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public string? RawStateJson { get; }

    public LiveSessionGameStateEvent(Guid sessionId, string? rawStateJson)
    {
        SessionId = sessionId;
        RawStateJson = rawStateJson;
    }
}
