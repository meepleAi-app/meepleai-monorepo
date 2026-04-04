using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

public record HealthStatusChangedEvent(
    string ServiceName,
    string PreviousStatus,
    string CurrentStatus,
    string? Description,
    string[] Tags,
    DateTime TransitionAt,
    bool IsReminder
) : INotification;
