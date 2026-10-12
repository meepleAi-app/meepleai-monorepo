namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il round derivato (spec §5.1): un giro completo del tavolo. <c>LiveGameSession</c> ha solo
/// l'indice di turno, che cresce a ogni turno di ogni giocatore.
/// </summary>
internal static class AssistantRound
{
    public static int Current(int currentTurnIndex, int activePlayers) =>
        activePlayers <= 0 ? 1 : (Math.Max(currentTurnIndex, 0) / activePlayers) + 1;
}
