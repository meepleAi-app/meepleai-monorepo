using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Issue #5477: Published when a critical infrastructure component (e.g. Redis)
/// becomes unavailable, causing subsystems (e.g. rate limiting) to degrade.
/// </summary>
internal sealed record InfrastructureDegradedEvent(
    string Component,
    string Subsystem,
    string Message,
    bool IsRecovered,
    DateTime OccurredAt) : INotification;
