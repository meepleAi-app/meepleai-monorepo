namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Il round derivato (spec §5.1): un giro completo del tavolo. <c>LiveGameSession</c> ha solo l'indice
/// di turno, che vale 0 prima dell'avvio, 1 con <c>Start()</c>, e cresce a ogni turno di ogni giocatore.
/// </summary>
internal static class AssistantRound
{
    /// <param name="currentTurnIndex">L'indice di turno del dominio, a base 1 (0 prima dell'avvio).</param>
    /// <param name="playersInTurnOrder">I giocatori attivi non spettatori: quelli fra cui gira il turno.</param>
    public static int Current(int currentTurnIndex, int playersInTurnOrder) =>
        playersInTurnOrder <= 0 || currentTurnIndex <= 1 ? 1 : ((currentTurnIndex - 1) / playersInTurnOrder) + 1;
}
