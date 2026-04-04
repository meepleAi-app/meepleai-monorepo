using Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;
using Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;

/// <summary>
/// Maps WhiteboardState domain entities to DTOs.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal static class WhiteboardMapper
{
    internal static WhiteboardStateDto ToDto(WhiteboardState whiteboardState)
    {
        var strokes = whiteboardState.Strokes
            .Select(s => new WhiteboardStrokeDto(s.Id, s.DataJson))
            .ToList()
            .AsReadOnly();

        return new WhiteboardStateDto(
            Id: whiteboardState.Id,
            SessionId: whiteboardState.SessionId,
            Strokes: strokes,
            StructuredJson: whiteboardState.StructuredJson,
            LastModifiedBy: whiteboardState.LastModifiedBy,
            LastModifiedAt: whiteboardState.LastModifiedAt,
            CreatedAt: whiteboardState.CreatedAt);
    }
}
