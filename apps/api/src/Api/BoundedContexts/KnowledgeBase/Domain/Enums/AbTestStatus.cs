namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Status of an A/B test session.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
public enum AbTestStatus
{
    /// <summary>Test created but responses not yet generated.</summary>
    Draft,

    /// <summary>Responses generated, awaiting evaluation.</summary>
    InProgress,

    /// <summary>All variants have been scored and models revealed.</summary>
    Evaluated,
}
