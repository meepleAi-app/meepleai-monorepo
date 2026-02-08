using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// ISSUE-3759: Validate move using Arbitro agent with real-time rules arbitration.
/// Routes to orchestration-service for LangGraph-based rule validation.
/// </summary>
internal record ValidateMoveCommand(
    Guid GameId,
    Guid SessionId,
    string Move,
    string GameState
) : IRequest<ValidateMoveResponse>;

/// <summary>
/// Response from Arbitro agent move validation.
/// </summary>
internal record ValidateMoveResponse(
    bool IsValid,
    string Reason,
    List<Guid> AppliedRuleIds,
    double Confidence,
    List<string> Citations,
    double ExecutionTimeMs,
    string? ErrorMessage = null
);
