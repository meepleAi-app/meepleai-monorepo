namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Stato di una versione di <see cref="Entities.AgentProfile"/> (ADR-095 D2).
/// Una versione si muove solo in avanti: Draft → Published → Archived.
/// </summary>
public enum AgentProfileVersionStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}
