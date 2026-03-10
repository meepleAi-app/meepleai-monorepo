using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Issue #5503: Triggers an immediate model availability check.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
public sealed record TriggerModelAvailabilityCheckCommand : ICommand<TriggerModelAvailabilityCheckResult>;

public sealed record TriggerModelAvailabilityCheckResult(bool Triggered, string Message);
