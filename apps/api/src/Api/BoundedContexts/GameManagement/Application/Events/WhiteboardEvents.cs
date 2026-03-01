using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Events;

/// <summary>
/// SSE event broadcast when a freehand stroke is added to the whiteboard.
/// Published with event type "session:whiteboard".
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed class StrokeAddedEvent : INotification
{
    public Guid SessionId { get; init; }
    public string StrokeId { get; init; }
    public string DataJson { get; init; }
    public Guid ModifiedBy { get; init; }

    public StrokeAddedEvent(Guid sessionId, string strokeId, string dataJson, Guid modifiedBy)
    {
        SessionId = sessionId;
        StrokeId = strokeId;
        DataJson = dataJson;
        ModifiedBy = modifiedBy;
    }
}

/// <summary>
/// SSE event broadcast when a freehand stroke is removed from the whiteboard.
/// Published with event type "session:whiteboard".
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed class StrokeRemovedEvent : INotification
{
    public Guid SessionId { get; init; }
    public string StrokeId { get; init; }
    public Guid ModifiedBy { get; init; }

    public StrokeRemovedEvent(Guid sessionId, string strokeId, Guid modifiedBy)
    {
        SessionId = sessionId;
        StrokeId = strokeId;
        ModifiedBy = modifiedBy;
    }
}

/// <summary>
/// SSE event broadcast when the structured layer (tokens, grid) is updated.
/// Published with event type "session:whiteboard".
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed class StructuredUpdatedEvent : INotification
{
    public Guid SessionId { get; init; }
    public string StructuredJson { get; init; }
    public Guid ModifiedBy { get; init; }

    public StructuredUpdatedEvent(Guid sessionId, string structuredJson, Guid modifiedBy)
    {
        SessionId = sessionId;
        StructuredJson = structuredJson;
        ModifiedBy = modifiedBy;
    }
}

/// <summary>
/// SSE event broadcast when the whiteboard is fully cleared.
/// Published with event type "session:whiteboard".
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed class WhiteboardClearedEvent : INotification
{
    public Guid SessionId { get; init; }
    public Guid ClearedBy { get; init; }

    public WhiteboardClearedEvent(Guid sessionId, Guid clearedBy)
    {
        SessionId = sessionId;
        ClearedBy = clearedBy;
    }
}
