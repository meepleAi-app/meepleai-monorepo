using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Issue #3481: Domain event raised when game publication status changes.
/// Enables future integrations: notifications, audit logs, SharedGameCatalog sync.
/// </summary>
internal sealed class GamePublishedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the published game.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the new approval status.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; }

    /// <summary>
    /// Gets the timestamp when approved (null if not approved).
    /// </summary>
    public DateTime? PublishedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GamePublishedEvent"/> class.
    /// </summary>
    public GamePublishedEvent(Guid gameId, ApprovalStatus approvalStatus, DateTime? publishedAt)
    {
        GameId = gameId;
        ApprovalStatus = approvalStatus;
        PublishedAt = publishedAt;
    }
}
