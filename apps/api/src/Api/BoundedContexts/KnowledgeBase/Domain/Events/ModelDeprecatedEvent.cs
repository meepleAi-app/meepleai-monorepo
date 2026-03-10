using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Issue #5496: Raised when an LLM model is detected as deprecated or unavailable.
/// Published by ModelAvailabilityCheckJob; handled by notification and auto-fallback handlers.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed record ModelDeprecatedEvent(
    string ModelId,
    string Provider,
    string[] AffectedStrategies,
    string? SuggestedReplacement,
    string Reason,
    DateTime DetectedAt) : INotification;
