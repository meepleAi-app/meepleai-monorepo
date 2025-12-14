using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing the response from an agent invocation.
/// </summary>
public sealed record AgentResponseDto
{
    /// <summary>
    /// Unique identifier for this invocation.
    /// </summary>
    public Guid InvocationId { get; init; }

    /// <summary>
    /// ID of the agent that processed the query.
    /// </summary>
    public Guid AgentId { get; init; }

    /// <summary>
    /// Name of the agent.
    /// </summary>
    public string AgentName { get; init; } = default!;

    /// <summary>
    /// Type of agent (RAG, Citation, Confidence, etc.).
    /// </summary>
    public string AgentType { get; init; } = default!;

    /// <summary>
    /// Original user query.
    /// </summary>
    public string Query { get; init; } = default!;

    /// <summary>
    /// Search results returned by the agent.
    /// </summary>
    public IReadOnlyList<SearchResultDto> Results { get; init; } = Array.Empty<SearchResultDto>();

    /// <summary>
    /// Overall confidence score (0.0-1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Quality level indicator.
    /// </summary>
    public string QualityLevel { get; init; } = default!;

    /// <summary>
    /// Timestamp when the agent was invoked.
    /// </summary>
    public DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Number of results returned.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// Creates a DTO from domain AgentInvocationResult.
    /// </summary>
    public static AgentResponseDto FromDomain(AgentInvocationResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        var qualityLevel = result.Confidence.Value switch
        {
            >= 0.80 => "High",
            >= 0.50 => "Medium",
            _ => "Low"
        };

        return new AgentResponseDto
        {
            InvocationId = result.InvocationId,
            AgentId = result.AgentId,
            AgentName = result.AgentName,
            AgentType = result.AgentType.Value,
            Query = result.Query,
            Results = result.SearchResults
                .Select(SearchResultDto.FromDomain)
                .ToList(),
            Confidence = result.Confidence.Value,
            QualityLevel = qualityLevel,
            ExecutedAt = result.ExecutedAt,
            ResultCount = result.SearchResults.Count
        };
    }
}
