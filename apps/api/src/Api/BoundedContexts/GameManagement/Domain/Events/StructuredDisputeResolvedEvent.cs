using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a structured rule dispute has been fully resolved
/// (votes tallied, outcome determined).
/// </summary>
internal sealed class StructuredDisputeResolvedEvent : DomainEventBase
{
    public Guid DisputeId { get; }
    public Guid SessionId { get; }
    public Guid GameId { get; }
    public DisputeVerdict Verdict { get; }
    public DisputeOutcome FinalOutcome { get; }
    public string? OverrideRule { get; }

    public StructuredDisputeResolvedEvent(
        Guid disputeId,
        Guid sessionId,
        Guid gameId,
        DisputeVerdict verdict,
        DisputeOutcome finalOutcome,
        string? overrideRule)
    {
        DisputeId = disputeId;
        SessionId = sessionId;
        GameId = gameId;
        Verdict = verdict;
        FinalOutcome = finalOutcome;
        OverrideRule = overrideRule;
    }
}
