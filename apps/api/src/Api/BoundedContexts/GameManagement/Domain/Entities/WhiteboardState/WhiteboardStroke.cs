namespace Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;

/// <summary>
/// Represents an individual freehand stroke on the whiteboard.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed record WhiteboardStroke(string Id, string DataJson);
