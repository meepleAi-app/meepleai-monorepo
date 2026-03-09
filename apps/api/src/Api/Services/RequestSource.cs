namespace Api.Services;

/// <summary>
/// Identifies the origin of an LLM request for observability and cost attribution.
/// Issue #5071: RequestSource tagging for OpenRouter monitoring.
/// </summary>
public enum RequestSource
{
    /// <summary>
    /// Direct user interaction (chat, playground, Q&amp;A). Default value.
    /// </summary>
    Manual,

    /// <summary>
    /// Retrieval-Augmented Generation pipeline (automated retrieval and synthesis).
    /// </summary>
    RagPipeline,

    /// <summary>
    /// System event handler triggering an LLM call in response to domain events.
    /// </summary>
    EventDriven,

    /// <summary>
    /// Automated test suite (unit, integration, E2E).
    /// </summary>
    AutomatedTest,

    /// <summary>
    /// Background agent task (chess engine, decision/arbitration agents, multi-model evaluation).
    /// </summary>
    AgentTask,

    /// <summary>
    /// Admin-initiated operation (health checks, debug console, setup guides for admin users).
    /// </summary>
    AdminOperation,

    /// <summary>
    /// A/B testing playground — blind model comparison for editors/admins.
    /// Issue #5505: Separate budget isolation and rate limiting.
    /// </summary>
    ABTesting,
}
