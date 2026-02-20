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
