using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - the directory, its reader and their mapping
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// <see cref="IAssistantSessionDirectory"/> sopra le query di GameManagement (spec §3.2). È qui, e non
/// negli strumenti, che vive il mediator: e solo per leggere.
/// </summary>
internal sealed class LiveSessionAssistantDirectory(IMediator mediator) : IAssistantSessionDirectory
{
    public async Task<AssistantSessionAccess> GetAccessAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetLiveSessionQuery(liveSessionId), cancellationToken).ConfigureAwait(false);
        return new AssistantSessionAccess(
            session.CreatedByUserId,
            session.Status.ToString(),
            // Anche Created: MoveToSetup non ha chiamanti, quindi senza Created l'assistente non
            // risponderebbe mai prima dell'avvio (scostamento S15 del piano 2).
            AcceptsQuestions: session.Status != LiveSessionStatus.Completed,
            GameId: session.GameId);
    }

    public ISessionAssistantReader CreateReader(Guid liveSessionId, Guid ownerUserId) =>
        new LiveSessionAssistantReader(mediator, liveSessionId, ownerUserId);
}

/// <summary>Lo stato vivo della sessione, riletto a ogni chiamata: gli strumenti non lo tengono in memoria.</summary>
internal sealed class LiveSessionAssistantReader(IMediator mediator, Guid liveSessionId, Guid ownerUserId) : ISessionAssistantReader
{
    public async Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetLiveSessionQuery(liveSessionId), cancellationToken).ConfigureAwait(false);
        var phases = await mediator.Send(new GetTurnPhasesQuery(liveSessionId), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> houseRules = [];
        if (session.GameId is { } gameId)
        {
            var memory = await mediator.Send(new GetGameMemoryQuery(gameId, ownerUserId), cancellationToken).ConfigureAwait(false);
            houseRules = memory?.HouseRules.Select(rule => rule.Description).ToList() ?? [];
        }

        return LiveSessionAssistantState.From(session, phases.CurrentPhaseName, houseRules);
    }
}

internal static class LiveSessionAssistantState
{
    public static AssistantSessionState From(LiveSessionDto session, string? phase, IReadOnlyList<string> houseRules)
    {
        // I giocatori fra cui gira il turno: gli attivi non spettatori. Il dominio conta sull'ordine di
        // turno, che il DTO non espone: con l'ordine predefinito (quello di ingresso) coincidono.
        var inTurnOrder = session.Players.Where(p => p.IsActive && p.Role != PlayerRole.Spectator).ToList();
        return new AssistantSessionState(
            session.GameId,
            string.IsNullOrWhiteSpace(session.GameName) ? null : session.GameName,
            session.Status.ToString(),
            CanRecordScores: session.Status is LiveSessionStatus.InProgress or LiveSessionStatus.Paused,
            CanAdvanceTurn: session.Status == LiveSessionStatus.InProgress,
            inTurnOrder.Select(p => new AssistantPlayer(p.Id, p.DisplayName, p.TotalScore, p.IsActive)).ToList(),
            session.CurrentTurnPlayerId,
            session.CurrentTurnIndex,
            AssistantRound.Current(session.CurrentTurnIndex, inTurnOrder.Count),
            phase,
            session.ScoringConfig.EnabledDimensions,
            session.RoundScores.Select(s => new AssistantScoreCell(s.PlayerId, s.Round, s.Dimension, s.Value)).ToList(),
            houseRules);
    }
}
