using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a shared game publication is rejected.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class SharedGamePublicationRejectedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game whose publication was rejected.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the ID of the admin who rejected the publication.
    /// </summary>
    public Guid RejectedBy { get; }

    /// <summary>
    /// Gets the reason for the rejection.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedGamePublicationRejectedEvent"/> class.
    /// </summary>
    public SharedGamePublicationRejectedEvent(Guid gameId, Guid rejectedBy, string reason)
    {
        GameId = gameId;
        RejectedBy = rejectedBy;
        Reason = reason;
    }
}
