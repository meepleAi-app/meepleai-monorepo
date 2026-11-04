namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>Che cosa propone l'assistente in partita (spec 2026-10-11 §5.2). I valori sono persistiti.</summary>
public enum AssistantProposalKind
{
    Score = 0,
    AdvanceTurn = 1,
}
