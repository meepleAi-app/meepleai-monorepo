using Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Whiteboard;

/// <summary>
/// Gets the current WhiteboardState for a session.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed record GetWhiteboardStateQuery(Guid SessionId) : IQuery<WhiteboardStateDto>;
