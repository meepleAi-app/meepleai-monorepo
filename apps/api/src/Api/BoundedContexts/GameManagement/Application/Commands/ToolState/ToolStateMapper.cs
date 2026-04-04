using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs.ToolState;

namespace Api.BoundedContexts.GameManagement.Application.Commands.ToolState;

internal static class ToolStateMapper
{
    public static ToolStateDto ToDto(Domain.Entities.ToolState.ToolState toolState)
    {
        using var doc = JsonDocument.Parse(toolState.StateDataJson);
        return new ToolStateDto(
            Id: toolState.Id,
            SessionId: toolState.SessionId,
            ToolkitId: toolState.ToolkitId,
            ToolName: toolState.ToolName,
            ToolType: toolState.ToolType,
            StateData: doc.RootElement.Clone(),
            CreatedAt: toolState.CreatedAt,
            LastUpdatedAt: toolState.LastUpdatedAt);
    }
}
