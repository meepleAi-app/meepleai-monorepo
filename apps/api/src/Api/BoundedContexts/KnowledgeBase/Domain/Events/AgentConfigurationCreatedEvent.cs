using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Event raised when an agent configuration is created.
/// Issue #2391 Sprint 2
/// </summary>
public record AgentConfigurationCreatedEvent(
    Guid ConfigurationId,
    Guid AgentId,
    string LlmModel,
    int AgentMode) : INotification;
