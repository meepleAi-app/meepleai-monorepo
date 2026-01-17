using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game is submitted for approval.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class SharedGameSubmittedForApprovalEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game submitted for approval.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the user who submitted the game for approval.
    /// </summary>
    public Guid SubmittedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGameSubmittedForApprovalEvent"/> class.
    /// </summary>
    public SharedGameSubmittedForApprovalEvent(Guid gameId, Guid submittedBy)
    {
        GameId = gameId;
        SubmittedBy = submittedBy;
    }
}
