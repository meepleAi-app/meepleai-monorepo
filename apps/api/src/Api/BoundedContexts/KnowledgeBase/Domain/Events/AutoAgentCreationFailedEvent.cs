using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Notification raised when AutoCreateAgentOnPdfReadyHandler fails to auto-create
/// an agent for a successfully indexed PDF (e.g., user hit tier-quota agent slot,
/// no system definition available, command handler error).
///
/// Issue #902 (SG1): Replaces the previous silent-failure swallow in the catch block.
/// Downstream consumers (UserNotifications BC) can subscribe to surface this to the
/// user explicitly via in-app notification or email — instead of leaving them confused
/// about why no agent appeared after PDF processing completed.
///
/// Issue #1937 / CF-1: Implements <see cref="IDomainEvent"/> so the notification dispatcher
/// can dedup on <c>EventId</c> when the publishing transaction rolls back after publish
/// (or MediatR retries transiently).
///
/// Carry-forward: actual user-visible notification delivery (in-app + email channel)
/// is a follow-up enhancement; this event ensures the failure is now observable
/// (logs + integration tests + future notification handlers).
/// </summary>
internal sealed record AutoAgentCreationFailedEvent(
    Guid PdfDocumentId,
    Guid GameId,
    Guid UserId,
    string ErrorCode,
    string Reason
) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
