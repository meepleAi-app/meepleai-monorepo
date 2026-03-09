using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Issue #5476: Raised when an admin activates or deactivates an emergency override.
/// Handled by event handler for audit logging and admin notifications.
/// </summary>
internal sealed record EmergencyOverrideActivatedEvent(
    string Action,
    int DurationMinutes,
    string Reason,
    Guid AdminUserId,
    string? TargetProvider,
    bool IsDeactivation,
    DateTime OccurredAt) : INotification;
