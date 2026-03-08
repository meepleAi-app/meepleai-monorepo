using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a widget's runtime state is updated within a session.
/// Broadcasted via SSE so all participants receive the latest widget state in real time.
/// Issue #5148 — Epic B5 (P2-1: Widget State SSE Broadcast).
/// </summary>
public record WidgetStateUpdatedEvent(
    Guid SessionId,
    Guid ToolkitId,
    string WidgetType,
    string StateJson,
    Guid UpdatedByUserId,
    DateTime Timestamp) : INotification;
