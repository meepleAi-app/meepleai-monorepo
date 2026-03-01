using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;

/// <summary>
/// Response DTO for GET /live-sessions/{sessionId}/tools.
/// Returns the four implicit base tools plus any custom ToolState items
/// initialized from a linked GameToolkit.
/// Issue #4969: Base Toolkit Layer.
/// </summary>
internal sealed record SessionToolsDto(
    Guid SessionId,
    Guid? ToolkitId,
    BaseToolkitDto BaseTools,
    IReadOnlyList<ToolStateDto> CustomTools);

/// <summary>
/// The four base tools always present in any session regardless of toolkit.
/// Override flags (wired in #4972) control IsAvailable per tool.
/// </summary>
internal sealed record BaseToolkitDto(
    BaseToolDto TurnOrder,
    BaseToolDto DiceSet,
    BaseToolDto Whiteboard,
    BaseToolDto Scoreboard);

/// <summary>
/// Represents a single implicit base tool entry.
/// </summary>
internal sealed record BaseToolDto(
    string ToolId,
    string Name,
    BaseToolType ToolType,
    bool IsAvailable,
    JsonElement DefaultConfig);

/// <summary>
/// Identifies the four always-present base tools.
/// </summary>
internal enum BaseToolType
{
    TurnOrder = 0,
    DiceSet = 1,
    Whiteboard = 2,
    Scoreboard = 3,
}
