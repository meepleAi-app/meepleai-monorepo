using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Issue #1292 (AC-6.2): raised when a new gamebook campaign session is
/// created. Triggers gamebook index cache invalidation for the owner so the
/// next <c>GET /api/v1/gamebooks</c> reflects the new campaign within 500ms.
/// </summary>
public record GamebookCampaignCreatedDomainEvent(Guid CampaignId, Guid GameId, Guid OwnerUserId) : INotification;

/// <summary>
/// Issue #1292 (AC-6.2): raised when a gamebook campaign session is
/// soft-deleted. Triggers gamebook index cache invalidation for the owner so
/// the deleted entry disappears from the next list response.
/// </summary>
public record GamebookCampaignDeletedDomainEvent(Guid CampaignId, Guid GameId, Guid OwnerUserId) : INotification;
