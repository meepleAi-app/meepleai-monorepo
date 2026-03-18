using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when votes have been cast on a dispute verdict.
/// </summary>
internal sealed class DisputeVotesCastEvent : DomainEventBase
{
    public Guid DisputeId { get; }
    public Guid SessionId { get; }
    public List<DisputeVote> Votes { get; }

    public DisputeVotesCastEvent(Guid disputeId, Guid sessionId, List<DisputeVote> votes)
    {
        DisputeId = disputeId;
        SessionId = sessionId;
        Votes = votes;
    }
}
