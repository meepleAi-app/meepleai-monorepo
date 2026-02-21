using Api.BoundedContexts.GameManagement.Application.DTOs.Whiteboard;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Whiteboard;

/// <summary>
/// Initializes a new empty WhiteboardState for a session.
/// Fails with ConflictException if already initialized.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed record InitializeWhiteboardCommand(
    Guid SessionId,
    Guid UserId) : ICommand<WhiteboardStateDto>;

/// <summary>
/// Adds a freehand stroke to the whiteboard.
/// Fails with ConflictException if a stroke with the same ID already exists.
/// </summary>
internal sealed record AddStrokeCommand(
    Guid SessionId,
    string StrokeId,
    string DataJson,
    Guid UserId) : ICommand<WhiteboardStateDto>;

/// <summary>
/// Removes a freehand stroke from the whiteboard by its ID.
/// Fails with NotFoundException if the stroke does not exist.
/// </summary>
internal sealed record RemoveStrokeCommand(
    Guid SessionId,
    string StrokeId,
    Guid UserId) : ICommand<WhiteboardStateDto>;

/// <summary>
/// Replaces the structured layer (token positions, grid config) of the whiteboard.
/// Validates max 100 KB payload.
/// </summary>
internal sealed record UpdateStructuredCommand(
    Guid SessionId,
    string StructuredJson,
    Guid UserId) : ICommand<WhiteboardStateDto>;

/// <summary>
/// Clears all strokes and resets the structured layer to "{}".
/// </summary>
internal sealed record ClearWhiteboardCommand(
    Guid SessionId,
    Guid UserId) : ICommand<WhiteboardStateDto>;
