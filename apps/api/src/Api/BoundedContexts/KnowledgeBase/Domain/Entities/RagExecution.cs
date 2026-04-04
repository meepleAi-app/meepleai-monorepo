using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Persisted RAG pipeline execution for historical browsing and debugging.
/// Issue #4458: RAG Execution History
/// </summary>
public sealed class RagExecution : AggregateRoot<Guid>
{
    private string _query = string.Empty;
    private string _strategy = string.Empty;
    private string _status = string.Empty;
    private string? _executionTrace;

    // Core fields
    public string Query => _query;
    public Guid? AgentDefinitionId { get; private set; }
    public string? AgentName { get; private set; }
    public string Strategy => _strategy;
    public string? Model { get; private set; }
    public string? Provider { get; private set; }
    public Guid? GameId { get; private set; }
    public bool IsPlayground { get; private set; }

    // Metrics
    public int TotalLatencyMs { get; private set; }
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public int TotalTokens { get; private set; }
    public decimal TotalCost { get; private set; }
    public double? Confidence { get; private set; }
    public bool CacheHit { get; private set; }
    public string? CragVerdict { get; private set; }

    // Status
    public string Status => _status;
    public string? ErrorMessage { get; private set; }

    // Trace data (JSONB)
    public string? ExecutionTrace => _executionTrace;

    // Timestamps
    public DateTime CreatedAt { get; private set; }

    // EF Core constructor
    private RagExecution() : base() { }

    public static RagExecution Create(
        string query,
        Guid? agentDefinitionId,
        string? agentName,
        string strategy,
        string? model,
        string? provider,
        Guid? gameId,
        bool isPlayground,
        int totalLatencyMs,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        decimal totalCost,
        double? confidence,
        bool cacheHit,
        string status,
        string? errorMessage,
        string? executionTrace,
        string? cragVerdict = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return new RagExecution
        {
            Id = Guid.NewGuid(),
            _query = query.Length > 2000 ? query[..2000] : query,
            AgentDefinitionId = agentDefinitionId,
            AgentName = agentName,
            _strategy = strategy,
            Model = model,
            Provider = provider,
            GameId = gameId,
            IsPlayground = isPlayground,
            TotalLatencyMs = totalLatencyMs,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            TotalCost = totalCost,
            Confidence = confidence,
            CacheHit = cacheHit,
            CragVerdict = cragVerdict,
            _status = status,
            ErrorMessage = errorMessage,
            _executionTrace = executionTrace,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
