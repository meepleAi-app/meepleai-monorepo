using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.ToolState;

namespace Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;

/// <summary>
/// DTO for ToolState entity.
/// </summary>
internal sealed record ToolStateDto(
    Guid Id,
    Guid SessionId,
    Guid ToolkitId,
    string ToolName,
    ToolType ToolType,
    JsonElement StateData,
    DateTime CreatedAt,
    DateTime LastUpdatedAt);

/// <summary>
/// Request DTO for rolling dice. ToolName comes from route parameter.
/// </summary>
internal sealed record RollDiceRequest(Guid? PlayerId);

/// <summary>
/// Request DTO for updating a counter. ToolName comes from route parameter.
/// </summary>
internal sealed record UpdateCounterRequest(string PlayerId, int Change);

/// <summary>
/// Request DTO for initializing tool states from a toolkit.
/// </summary>
internal sealed record InitializeToolStatesRequest(Guid ToolkitId);
