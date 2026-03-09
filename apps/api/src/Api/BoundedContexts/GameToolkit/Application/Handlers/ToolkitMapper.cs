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
            PrivateGameId: toolkit.PrivateGameId,
            Name: toolkit.Name,
            Version: toolkit.Version,
            CreatedByUserId: toolkit.CreatedByUserId,
            IsPublished: toolkit.IsPublished,
            OverridesTurnOrder: toolkit.OverridesTurnOrder,
            OverridesScoreboard: toolkit.OverridesScoreboard,
            OverridesDiceSet: toolkit.OverridesDiceSet,
            CreatedAt: toolkit.CreatedAt,
            UpdatedAt: toolkit.UpdatedAt,
            DiceTools: toolkit.DiceTools.Select(d => new DiceToolDto(
                d.Name, d.DiceType, d.Quantity, d.CustomFaces, d.IsInteractive, d.Color
            )).ToList(),
            CardTools: toolkit.CardTools.Select(c => new CardToolDto(
                c.Name, c.DeckType, c.CardCount, c.Shuffleable,
                c.DefaultZone, c.DefaultOrientation,
                c.CardEntries.Select(e => new CardEntryDto(e.Name, e.Suit, e.Rank, e.CustomData)).ToList(),
                c.AllowDraw, c.AllowDiscard, c.AllowPeek, c.AllowReturnToDeck
            )).ToList(),
            TimerTools: toolkit.TimerTools.Select(t => new TimerToolDto(
                t.Name, t.DurationSeconds, t.TimerType, t.AutoStart, t.Color,
                t.IsPerPlayer, t.WarningThresholdSeconds
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
            StateTemplate: toolkit.StateTemplate != null
                ? new StateTemplateDto(
                    toolkit.StateTemplate.Name,
                    toolkit.StateTemplate.Description,
                    toolkit.StateTemplate.Category,
                    toolkit.StateTemplate.SchemaJson)
                : null,
            AgentConfig: toolkit.AgentConfig,
            TemplateStatus: toolkit.TemplateStatus,
            IsTemplate: toolkit.IsTemplate,
            ReviewNotes: toolkit.ReviewNotes,
            ReviewedByUserId: toolkit.ReviewedByUserId,
            ReviewedAt: toolkit.ReviewedAt
        );
    }
}
