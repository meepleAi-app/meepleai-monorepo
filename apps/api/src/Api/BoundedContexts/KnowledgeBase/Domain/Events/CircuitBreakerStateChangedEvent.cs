using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// ISSUE-5086: Raised when an LLM provider circuit breaker transitions between states.
/// Published fire-and-forget by HybridLlmService; handled by CircuitBreakerStateChangedEventHandler.
/// </summary>
internal sealed record CircuitBreakerStateChangedEvent(
    string Provider,
    CircuitState PreviousState,
    CircuitState NewState,
    string Reason,
    DateTime OccurredAt) : INotification;
