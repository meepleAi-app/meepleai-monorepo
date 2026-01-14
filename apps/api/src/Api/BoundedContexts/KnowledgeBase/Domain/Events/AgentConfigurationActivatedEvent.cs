using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Event raised when a configuration is set as current.
/// Issue #2391 Sprint 2
/// </summary>
public record AgentConfigurationActivatedEvent(
    Guid ConfigurationId,
    Guid AgentId) : INotification;
