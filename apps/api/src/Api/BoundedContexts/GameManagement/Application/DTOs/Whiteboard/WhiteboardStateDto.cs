namespace Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;

/// <summary>
/// Response DTO for an individual whiteboard stroke.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed record WhiteboardStrokeDto(string Id, string DataJson);

/// <summary>
/// Response DTO for the full WhiteboardState.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed record WhiteboardStateDto(
    Guid Id,
    Guid SessionId,
    IReadOnlyList<WhiteboardStrokeDto> Strokes,
    string StructuredJson,
    Guid LastModifiedBy,
    DateTime LastModifiedAt,
    DateTime CreatedAt);
