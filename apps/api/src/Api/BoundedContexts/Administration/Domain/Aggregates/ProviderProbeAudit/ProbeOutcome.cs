namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

/// <summary>
/// Outcome of a provider token probe attempt. ISSUE-936.
/// </summary>
internal enum ProbeOutcome
{
    Success = 0,
    NotConfigured = 1,    // env var missing or empty
    Unauthorized = 2,     // provider returned 401/403
    Timeout = 3,          // probe exceeded 5s
    Unreachable = 4,      // DNS / network failure
    ModelMissing = 5,     // configured model not in list
    UnknownError = 99
}
