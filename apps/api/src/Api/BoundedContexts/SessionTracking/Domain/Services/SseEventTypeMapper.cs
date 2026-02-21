using Api.BoundedContexts.GameManagement.Application.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Services;

/// <summary>
/// Maps domain events to typed SSE event names for frontend consumption.
/// Issue #4764 - SSE Streaming Infrastructure + Session State Broadcasting
/// </summary>
public static class SseEventTypeMapper
{
    private static readonly Dictionary<Type, string> EventTypeMap = new()
    {
        // Session lifecycle → session:state
        [typeof(SessionCreatedEvent)] = "session:state",
        [typeof(SessionPausedEvent)] = "session:state",
        [typeof(SessionResumedEvent)] = "session:state",
        [typeof(SessionFinalizedEvent)] = "session:state",

        // Score changes → session:score
        [typeof(ScoreUpdatedEvent)] = "session:score",

        // Player join/leave/kick/ready → session:player
        [typeof(ParticipantAddedEvent)] = "session:player",
        [typeof(ParticipantKickedEvent)] = "session:player",
        [typeof(PlayerReadyEvent)] = "session:player",

        // Role changes → session:player
        [typeof(ParticipantRoleChangedEvent)] = "session:player",

        // Conflict notifications → session:conflict
        [typeof(ConflictDetectedEvent)] = "session:conflict",

        // Toolkit: dice → session:toolkit
        [typeof(DiceRolledEvent)] = "session:toolkit",

        // Toolkit: cards → session:toolkit
        [typeof(CardsDrawnEvent)] = "session:toolkit",
        [typeof(CardsDiscardedEvent)] = "session:toolkit",
        [typeof(CardsRevealedEvent)] = "session:toolkit",
        [typeof(DeckShuffledEvent)] = "session:toolkit",
        [typeof(DeckCreatedEvent)] = "session:toolkit",
        [typeof(DeckResetEvent)] = "session:toolkit",

        // Chat → session:chat
        [typeof(SessionChatMessageSentEvent)] = "session:chat",

        // Notes → session:toolkit
        [typeof(NoteSavedEvent)] = "session:toolkit",
        [typeof(NoteRevealedEvent)] = "session:toolkit",
        [typeof(NoteHiddenEvent)] = "session:toolkit",
        [typeof(NoteUpdatedEvent)] = "session:toolkit",
        [typeof(NoteDeletedEvent)] = "session:toolkit",

        // Media → session:toolkit
        [typeof(SessionMediaUploadedEvent)] = "session:toolkit",
        [typeof(SessionMediaDeletedEvent)] = "session:toolkit",

        // Turn order → session:toolkit (Issue #4970)
        [typeof(TurnAdvancedEvent)] = "session:toolkit",
    };

    /// <summary>
    /// Gets the SSE event type name for a domain event.
    /// Falls back to "session:state" for unmapped events.
    /// </summary>
    public static string GetEventType(INotification evt)
    {
        return GetEventType(evt.GetType());
    }

    /// <summary>
    /// Gets the SSE event type name for a domain event type.
    /// </summary>
    public static string GetEventType(Type eventType)
    {
        return EventTypeMap.TryGetValue(eventType, out var sseType)
            ? sseType
            : "session:state";
    }

    /// <summary>
    /// Gets the SSE event type name for a generic domain event type.
    /// </summary>
    public static string GetEventType<T>() where T : INotification
    {
        return GetEventType(typeof(T));
    }
}
