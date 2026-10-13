namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Stati di una proposta (spec 2026-10-11 §5.2). <c>Pending</c> → <c>Applying</c> → <c>Accepted</c>,
/// <c>Edited</c> o <c>Failed</c>; da <c>Pending</c> anche <c>Rejected</c> ed <c>Expired</c>; <c>Stale</c>
/// da <c>Pending</c> o da <c>Applying</c>. I valori sono persistiti.
/// </summary>
public enum AssistantProposalStatus
{
    Pending = 0,
    Applying = 1,
    Accepted = 2,
    Edited = 3,
    Failed = 4,
    Rejected = 5,
    Expired = 6,
    Stale = 7,
}
