using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for AI agent orchestration and selection.
/// Determines which agent should handle a given query based on context and agent capabilities.
/// </summary>
/// <remarks>
/// Design Decision (ADR-004): Orchestration is a domain service within KnowledgeBase context.
/// Agent selection is based on query type classification and agent type specialization.
/// </remarks>
public class AgentOrchestrationService
{
    /// <summary>
    /// Selects the most appropriate agent for a given query from available agents.
    /// </summary>
    /// <param name="query">User query text</param>
    /// <param name="availableAgents">List of active agents to choose from</param>
    /// <returns>Selected agent, or null if no suitable agent found</returns>
    public Agent? SelectAgentForQuery(string query, List<Agent> availableAgents)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        if (availableAgents == null || availableAgents.Count == 0)
            return null;

        // Filter to active agents only
        var activeAgents = availableAgents.Where(a => a.IsActive).ToList();
        if (activeAgents.Count == 0)
            return null;

        // Classify query type
        var queryType = ClassifyQuery(query);

        // Select agent based on query type and agent specialization
        var selectedAgent = queryType switch
        {
            QueryType.RulesInterpretation => activeAgents
                .FirstOrDefault(a => string.Equals(a.Type.Value, AgentType.RulesInterpreter.Value, StringComparison.Ordinal)),

            QueryType.CitationVerification => activeAgents
                .FirstOrDefault(a => string.Equals(a.Type.Value, AgentType.CitationAgent.Value, StringComparison.Ordinal)),

            QueryType.ConfidenceAssessment => activeAgents
                .FirstOrDefault(a => string.Equals(a.Type.Value, AgentType.ConfidenceAgent.Value, StringComparison.Ordinal)),

            QueryType.ConversationContinuation => activeAgents
                .FirstOrDefault(a => string.Equals(a.Type.Value, AgentType.ConversationAgent.Value, StringComparison.Ordinal)),

            QueryType.GeneralQuestion => activeAgents
                .FirstOrDefault(a => string.Equals(a.Type.Value, AgentType.RagAgent.Value, StringComparison.Ordinal)),

            _ => null
        };

        // Fallback: Select any active RAG agent if no specialized match
        selectedAgent ??= activeAgents
            .FirstOrDefault(a => string.Equals(a.Type.Value, AgentType.RagAgent.Value, StringComparison.Ordinal));

        // Final fallback: Select the most recently used active agent
        selectedAgent ??= activeAgents
            .OrderByDescending(a => a.LastInvokedAt ?? DateTime.MinValue)
            .First();

        return selectedAgent;
    }

    /// <summary>
    /// Executes agent invocation using provided domain services.
    /// </summary>
    /// <param name="agent">Agent to invoke</param>
    /// <param name="context">Invocation context with query and embeddings</param>
    /// <param name="vectorSearch">Vector search domain service</param>
    /// <param name="qualityTracking">Quality tracking domain service</param>
    /// <returns>Agent execution results</returns>
    public AgentInvocationResult ExecuteAgent(
        Agent agent,
        AgentInvocationContext context,
        VectorSearchDomainService vectorSearch,
        QualityTrackingDomainService qualityTracking)
    {
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (vectorSearch == null)
            throw new ArgumentNullException(nameof(vectorSearch));

        if (qualityTracking == null)
            throw new ArgumentNullException(nameof(qualityTracking));

        // Ensure agent is active
        if (!agent.IsActive)
            throw new InvalidOperationException($"Cannot invoke inactive agent: {agent.Name}");

        // Extract strategy parameters
        var strategy = agent.Strategy;
        var topK = strategy.GetParameter("TopK", 10);
        var minScore = strategy.GetParameter("MinScore", 0.55);

        // Perform vector search using domain service
        var searchResults = vectorSearch.Search(
            queryVector: context.QueryVector,
            candidateEmbeddings: context.CandidateEmbeddings,
            topK: topK,
            minScore: minScore
        );

        // Calculate confidence using quality tracking service
        var confidence = qualityTracking.CalculateSearchConfidence(searchResults);

        // Record invocation on agent aggregate
        // Issue #1694: This service performs orchestration/vector search only (no LLM call).
        // LLM token tracking happens in RAG query handlers that call ILlmService.
        agent.RecordInvocation(context.Query, new TokenUsage(0, 0, 0, 0m, "none", "orchestration"));

        // Build result
        return new AgentInvocationResult(
            invocationId: context.InvocationId,
            agentId: agent.Id,
            agentName: agent.Name,
            agentType: agent.Type,
            searchResults: searchResults,
            confidence: confidence,
            query: context.Query,
            executedAt: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Classifies query type based on content analysis.
    /// </summary>
    private QueryType ClassifyQuery(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        // Citation verification patterns
        if (lowerQuery.Contains("source") || lowerQuery.Contains("citation") ||
            lowerQuery.Contains("where did") || lowerQuery.Contains("reference"))
        {
            return QueryType.CitationVerification;
        }

        // Confidence assessment patterns
        if (lowerQuery.Contains("how sure") || lowerQuery.Contains("confident") ||
            lowerQuery.Contains("certain") || lowerQuery.Contains("accuracy"))
        {
            return QueryType.ConfidenceAssessment;
        }

        // Conversation continuation patterns
        if (lowerQuery.StartsWith("and ", StringComparison.Ordinal) || lowerQuery.StartsWith("but ", StringComparison.Ordinal) ||
            lowerQuery.StartsWith("also ", StringComparison.Ordinal) || lowerQuery.Contains("you said") ||
            lowerQuery.Contains("earlier"))
        {
            return QueryType.ConversationContinuation;
        }

        // Rules interpretation patterns (game-specific keywords)
        if (lowerQuery.Contains("rule") || lowerQuery.Contains("legal move") ||
            lowerQuery.Contains("can i") || lowerQuery.Contains("allowed to") ||
            lowerQuery.Contains("phase") || lowerQuery.Contains("turn") ||
            lowerQuery.Contains("setup"))
        {
            return QueryType.RulesInterpretation;
        }

        // Default: General question
        return QueryType.GeneralQuestion;
    }
}

/// <summary>
/// Enum representing different query types for agent selection.
/// </summary>
public enum QueryType
{
    GeneralQuestion,
    RulesInterpretation,
    CitationVerification,
    ConfidenceAssessment,
    ConversationContinuation
}

/// <summary>
/// Value object representing the result of an agent invocation.
/// </summary>
public sealed record AgentInvocationResult
{
    public Guid InvocationId { get; init; }
    public Guid AgentId { get; init; }
    public string AgentName { get; init; }
    public AgentType AgentType { get; init; }
    public List<SearchResult> SearchResults { get; init; }
    public Confidence Confidence { get; init; }
    public string Query { get; init; }
    public DateTime ExecutedAt { get; init; }

    public AgentInvocationResult(
        Guid invocationId,
        Guid agentId,
        string agentName,
        AgentType agentType,
        List<SearchResult> searchResults,
        Confidence confidence,
        string query,
        DateTime executedAt)
    {
        InvocationId = invocationId;
        AgentId = agentId;
        AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        AgentType = agentType ?? throw new ArgumentNullException(nameof(agentType));
        SearchResults = searchResults ?? new List<SearchResult>();
        Confidence = confidence ?? throw new ArgumentNullException(nameof(confidence));
        Query = query ?? throw new ArgumentNullException(nameof(query));
        ExecutedAt = executedAt;
    }

    /// <summary>
    /// Checks if the result meets quality thresholds.
    /// </summary>
    public bool IsHighQuality => Confidence.Value >= 0.80;

    /// <summary>
    /// Checks if the result has low confidence.
    /// </summary>
    public bool IsLowConfidence => Confidence.Value < 0.50;

    /// <summary>
    /// Gets the number of search results returned.
    /// </summary>
    public int ResultCount => SearchResults.Count;
}
