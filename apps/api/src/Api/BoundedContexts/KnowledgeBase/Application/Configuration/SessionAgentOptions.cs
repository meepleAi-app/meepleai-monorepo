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

    /// <summary>
    /// #3390 Slice 2 — latency budget (in milliseconds) for the grounded RAG retrieval on the
    /// in-session IMAGE path (embedding + pgvector + rerank). When it expires, the handler
    /// degrades to the multimodal-only path (which stays Ungrounded, #3388) instead of making
    /// the user wait. The retrieval respects the caller's CancellationToken end-to-end, so the
    /// budget is enforced caller-side via a linked CTS + CancelAfter.
    /// Default: 700 ms.
    ///
    /// NEEDS TUNING: the live path is the hottest (user at the table) and the baseline P95 is
    /// already ~2500 ms. Observe the retrieval_profile=none fallback rate on
    /// meepleai.agent.response.grounding and the meepleai.rag.retrieval.fallbacks{fallback_type=retrieval_budget}
    /// counter before widening.
    /// </summary>
    public int RetrievalBudgetMs { get; set; } = 700;
}
