using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

internal static class ToolkitMapper
{
    public static GameToolkitDto ToDto(Domain.Entities.GameToolkit toolkit)
    {
        return new GameToolkitDto(
            Id: toolkit.Id,
            GameId: toolkit.GameId,
            Name: toolkit.Name,
            Version: toolkit.Version,
            CreatedByUserId: toolkit.CreatedByUserId,
            IsPublished: toolkit.IsPublished,
            CreatedAt: toolkit.CreatedAt,
            UpdatedAt: toolkit.UpdatedAt,
            DiceTools: toolkit.DiceTools.Select(d => new DiceToolDto(
                d.Name, d.DiceType, d.Quantity, d.CustomFaces, d.IsInteractive, d.Color
            )).ToList(),
            CardTools: toolkit.CardTools.Select(c => new CardToolDto(
                c.Name, c.DeckType, c.CardCount, c.Shuffleable
            )).ToList(),
            TimerTools: toolkit.TimerTools.Select(t => new TimerToolDto(
                t.Name, t.DurationSeconds, t.IsCountdown, t.AutoStart, t.Color
            )).ToList(),
            CounterTools: toolkit.CounterTools.Select(c => new CounterToolDto(
                c.Name, c.MinValue, c.MaxValue, c.DefaultValue, c.IsPerPlayer, c.Icon, c.Color
            )).ToList(),
            ScoringTemplate: toolkit.ScoringTemplate != null
                ? new ScoringTemplateDto(
                    toolkit.ScoringTemplate.Dimensions,
                    toolkit.ScoringTemplate.DefaultUnit,
                    toolkit.ScoringTemplate.ScoreType)
                : null,
            TurnTemplate: toolkit.TurnTemplate != null
                ? new TurnTemplateDto(
                    toolkit.TurnTemplate.TurnOrderType,
                    toolkit.TurnTemplate.Phases)
                : null,
            StateTemplate: toolkit.StateTemplate,
            AgentConfig: toolkit.AgentConfig
        );
    }
}
