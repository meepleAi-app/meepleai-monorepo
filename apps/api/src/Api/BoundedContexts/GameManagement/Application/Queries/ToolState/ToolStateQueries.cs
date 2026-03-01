using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.ToolState;

/// <summary>
/// Gets all tool states for a session.
/// </summary>
internal sealed record GetToolStatesQuery(Guid SessionId) : IQuery<IReadOnlyList<ToolStateDto>>;

/// <summary>
/// Gets a specific tool state by session and tool name.
/// </summary>
internal sealed record GetToolStateQuery(Guid SessionId, string ToolName) : IQuery<ToolStateDto?>;

/// <summary>
/// Gets the full toolkit for a session: four implicit base tools + custom ToolState items.
/// Issue #4969: Base Toolkit Layer.
/// </summary>
internal sealed record GetSessionToolsQuery(Guid SessionId) : IQuery<SessionToolsDto>;
