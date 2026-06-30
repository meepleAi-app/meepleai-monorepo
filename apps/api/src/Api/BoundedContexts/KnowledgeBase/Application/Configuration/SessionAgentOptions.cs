namespace Api.BoundedContexts.KnowledgeBase.Application.Configuration;

/// <summary>
/// Configuration for the session-agent chat handler (Issue #2600 — SP5-c).
/// Bound from the "SessionAgent" section of appsettings.json.
/// </summary>
internal sealed class SessionAgentOptions
{
    /// <summary>
    /// Maximum time (in seconds) allowed between consecutive LLM stream chunks
    /// before the handler treats the stream as hung and surfaces a timeout error.
    /// Default: 30 seconds.
    ///
    /// NEEDS TUNING: observe p99 inter-chunk latency in production before tightening.
    /// A per-chunk budget of 30 s is intentionally generous to avoid false positives
    /// on slow first-chunks from cold providers. Fractional values (e.g. 0.05) are
    /// valid and used in unit tests to avoid waiting 30 s.
    /// </summary>
    public double LlmPerChunkTimeoutSeconds { get; set; } = 30.0;
}
