using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Entity representing a persisted agent test result.
/// Issue #3379 - Agent Test Results History &amp; Persistence
/// </summary>
internal sealed class AgentTestResult : AggregateRoot<Guid>
{
    /// <summary>
    /// The typology that was tested.
    /// </summary>
    public Guid TypologyId { get; private set; }

    /// <summary>
    /// Optional strategy override used for the test (null means typology default).
    /// </summary>
    public string? StrategyOverride { get; private set; }

    /// <summary>
    /// The model used for the test.
    /// </summary>
    public string ModelUsed { get; private set; }

    /// <summary>
    /// The query that was tested.
    /// </summary>
    public string Query { get; private set; }

    /// <summary>
    /// The response from the agent.
    /// </summary>
    public string Response { get; private set; }

    /// <summary>
    /// Confidence score of the response (0.0 to 1.0).
    /// </summary>
    public double ConfidenceScore { get; private set; }

    /// <summary>
    /// Number of tokens used in the test.
    /// </summary>
    public int TokensUsed { get; private set; }

    /// <summary>
    /// Estimated cost of the test in USD.
    /// </summary>
    public decimal CostEstimate { get; private set; }

    /// <summary>
    /// Latency of the test in milliseconds.
    /// </summary>
    public int LatencyMs { get; private set; }

    /// <summary>
    /// Citations returned by the agent (JSON array).
    /// </summary>
    public string? CitationsJson { get; private set; }

    /// <summary>
    /// When the test was executed.
    /// </summary>
    public DateTime ExecutedAt { get; private set; }

    /// <summary>
    /// User who executed the test.
    /// </summary>
    public Guid ExecutedBy { get; private set; }

    /// <summary>
    /// Optional notes or comments about the test.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Whether this test result has been marked as a favorite/saved.
    /// </summary>
    public bool IsSaved { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private AgentTestResult() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new agent test result.
    /// </summary>
    public static AgentTestResult Create(
        Guid typologyId,
        string query,
        string response,
        string modelUsed,
        double confidenceScore,
        int tokensUsed,
        decimal costEstimate,
        int latencyMs,
        Guid executedBy,
        string? strategyOverride = null,
        string? citationsJson = null)
    {
        if (typologyId == Guid.Empty)
            throw new ArgumentException("Typology ID cannot be empty", nameof(typologyId));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response cannot be empty", nameof(response));
        if (string.IsNullOrWhiteSpace(modelUsed))
            throw new ArgumentException("Model used cannot be empty", nameof(modelUsed));
        if (confidenceScore < 0 || confidenceScore > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0 and 1");
        if (tokensUsed < 0)
            throw new ArgumentOutOfRangeException(nameof(tokensUsed), "Tokens used cannot be negative");
        if (costEstimate < 0)
            throw new ArgumentOutOfRangeException(nameof(costEstimate), "Cost estimate cannot be negative");
        if (latencyMs < 0)
            throw new ArgumentOutOfRangeException(nameof(latencyMs), "Latency cannot be negative");
        if (executedBy == Guid.Empty)
            throw new ArgumentException("Executed by cannot be empty", nameof(executedBy));

        var result = new AgentTestResult
        {
            Id = Guid.NewGuid(),
            TypologyId = typologyId,
            Query = query.Trim(),
            Response = response,
            ModelUsed = modelUsed,
            ConfidenceScore = confidenceScore,
            TokensUsed = tokensUsed,
            CostEstimate = costEstimate,
            LatencyMs = latencyMs,
            ExecutedBy = executedBy,
            StrategyOverride = strategyOverride,
            CitationsJson = citationsJson,
            ExecutedAt = DateTime.UtcNow,
            IsSaved = false
        };

        return result;
    }

    /// <summary>
    /// Marks this test result as saved/favorited.
    /// </summary>
    public void MarkAsSaved()
    {
        IsSaved = true;
    }

    /// <summary>
    /// Removes the saved/favorited status.
    /// </summary>
    public void RemoveSavedStatus()
    {
        IsSaved = false;
    }

    /// <summary>
    /// Adds notes to the test result.
    /// </summary>
    public void AddNotes(string notes)
    {
        Notes = notes?.Trim();
    }
}
