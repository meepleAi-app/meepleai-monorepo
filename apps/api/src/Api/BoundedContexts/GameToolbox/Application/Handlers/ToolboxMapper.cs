using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

namespace Api.BoundedContexts.GameToolbox.Application.Handlers;

internal static class ToolboxMapper
{
    public static ToolboxDto ToDto(Toolbox toolbox) => new(
        Id: toolbox.Id,
        Name: toolbox.Name,
        GameId: toolbox.GameId,
        Mode: toolbox.Mode.ToString(),
        SharedContext: ToDto(toolbox.SharedContext),
        CurrentPhaseId: toolbox.CurrentPhaseId,
        Tools: toolbox.Tools.Select(ToDto).OrderBy(t => t.Order).ToList(),
        Phases: toolbox.Phases.Select(ToDto).OrderBy(p => p.Order).ToList(),
        CreatedAt: toolbox.CreatedAt,
        UpdatedAt: toolbox.UpdatedAt);

    public static ToolboxToolDto ToDto(ToolboxTool tool) => new(
        Id: tool.Id,
        Type: tool.Type,
        Config: tool.Config,
        State: tool.State,
        IsEnabled: tool.IsEnabled,
        Order: tool.Order);

    public static PhaseDto ToDto(Phase phase) => new(
        Id: phase.Id,
        Name: phase.Name,
        Order: phase.Order,
        ActiveToolIds: phase.ActiveToolIds.ToList());

    public static SharedContextDto ToDto(SharedContext ctx) => new(
        Players: ctx.Players.Select(p => new PlayerInfoDto(p.Name, p.Color, p.AvatarUrl)).ToList(),
        CurrentPlayerIndex: ctx.CurrentPlayerIndex,
        CurrentRound: ctx.CurrentRound,
        CustomProperties: new Dictionary<string, string>(ctx.CustomProperties, StringComparer.Ordinal));

    public static SharedContext ToDomain(SharedContextDto dto) => new()
    {
        Players = dto.Players.Select(p => new PlayerInfo(p.Name, p.Color, p.AvatarUrl)).ToList(),
        CurrentPlayerIndex = dto.CurrentPlayerIndex,
        CurrentRound = dto.CurrentRound,
        CustomProperties = new Dictionary<string, string>(dto.CustomProperties, StringComparer.Ordinal)
    };

    public static SharedContext ToDomain(List<PlayerInfoDto> players, int currentPlayerIndex, int currentRound, Dictionary<string, string> customProperties) => new()
    {
        Players = players.Select(p => new PlayerInfo(p.Name, p.Color, p.AvatarUrl)).ToList(),
        CurrentPlayerIndex = currentPlayerIndex,
        CurrentRound = currentRound,
        CustomProperties = new Dictionary<string, string>(customProperties, StringComparer.Ordinal)
    };

    public static ToolboxTemplateDto ToDto(ToolboxTemplate tpl) => new(
        Id: tpl.Id,
        Name: tpl.Name,
        GameId: tpl.GameId,
        Mode: tpl.Mode.ToString(),
        Source: tpl.Source.ToString(),
        ToolsJson: tpl.ToolsJson,
        PhasesJson: tpl.PhasesJson,
        CreatedAt: tpl.CreatedAt);
}
