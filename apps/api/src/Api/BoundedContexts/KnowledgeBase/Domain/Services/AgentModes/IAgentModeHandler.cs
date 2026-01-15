#pragma warning disable MA0002 // Dictionary without StringComparer
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;

/// <summary>
/// Interface for handling agent-specific behavior based on AgentMode.
/// Implementations provide mode-specific logic for Chat, Player, and Ledger modes.
/// Issue #2391 - Agent Mode system
/// </summary>
internal interface IAgentModeHandler
{
    /// <summary>
    /// The AgentMode this handler supports
    /// </summary>
    AgentMode SupportedMode { get; }

    /// <summary>
    /// Processes the agent invocation with mode-specific logic
    /// </summary>
    /// <param name="context">Context containing agent, configuration, query, and search results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Mode-specific result with content and metadata</returns>
    Task<AgentModeResult> HandleAsync(
        AgentModeContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to agent mode handlers containing all necessary data
/// </summary>
/// <param name="Agent">The agent being invoked</param>
/// <param name="Configuration">Current agent configuration with mode, LLM settings, documents</param>
/// <param name="Query">User query/question</param>
/// <param name="GameId">Optional game context for Player/Ledger modes</param>
/// <param name="ChatThreadId">Optional chat thread for conversation context</param>
/// <param name="SearchResults">RAG search results from vector database</param>
/// <param name="UserId">User making the request</param>
internal record AgentModeContext(
    Agent Agent,
    AgentConfiguration Configuration,
    string Query,
    Guid? GameId,
    Guid? ChatThreadId,
    IReadOnlyList<SearchResult> SearchResults,
    Guid UserId);

/// <summary>
/// Result from agent mode handler processing
/// </summary>
internal sealed class AgentModeResult
{
    /// <summary>
    /// Mode that generated this result
    /// </summary>
    public AgentMode Mode { get; init; }

    /// <summary>
    /// Primary content/response for the user
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Confidence score (0.0 to 1.0) for the result
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Additional mode-specific metadata
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }
        = new Dictionary<string, object>();
}
