using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

namespace Api.Services.Providers.Probe;

internal sealed record ProbeExecutionResult(
    ProbeOutcome Outcome,
    string? ErrorCode,
    string? ErrorMessage,
    int LatencyMs,
    bool? ModelAvailable);   // null when expectedModel was not provided
