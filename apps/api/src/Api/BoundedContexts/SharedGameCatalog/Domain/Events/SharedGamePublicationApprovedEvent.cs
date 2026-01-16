using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game publication is approved.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class SharedGamePublicationApprovedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game whose publication was approved.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the admin who approved the publication.
    /// </summary>
    public Guid ApprovedBy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGamePublicationApprovedEvent"/> class.
    /// </summary>
    public SharedGamePublicationApprovedEvent(Guid gameId, Guid approvedBy)
    {
        GameId = gameId;
        ApprovedBy = approvedBy;
    }
}
