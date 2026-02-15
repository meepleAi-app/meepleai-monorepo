namespace Api.BoundedContexts.Administration.Domain.Aggregates.RagExecution;

/// <summary>
/// Aggregate root for RAG execution history records.
/// Stores the result of a pipeline execution including all SSE events.
/// Issue #4458: Execution History, Issue #4459: Query Replay.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "MA0049:Type name should not match containing namespace", Justification = "Conventional DDD aggregate naming")]
public sealed class RagExecution
{
    private RagExecution() { }

    public Guid Id { get; private set; }

    /// <summary>
    /// The pipeline strategy used for this execution.
    /// </summary>
    public Guid StrategyId { get; private set; }

    /// <summary>
    /// Snapshot of pipeline definition at execution time (JSONB).
    /// </summary>
    public string PipelineDefinitionJson { get; private set; } = "{}";

    /// <summary>
    /// The query that was executed.
    /// </summary>
    public string TestQuery { get; private set; } = string.Empty;

    /// <summary>
    /// User who triggered the execution.
    /// </summary>
    public Guid ExecutedByUserId { get; private set; }

    /// <summary>
    /// Whether execution completed successfully.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    /// Total execution duration in milliseconds.
    /// </summary>
    public int TotalDurationMs { get; private set; }

    /// <summary>
    /// Total tokens consumed.
    /// </summary>
    public int TotalTokensUsed { get; private set; }

    /// <summary>
    /// Total cost of execution.
    /// </summary>
    public decimal TotalCost { get; private set; }

    /// <summary>
    /// Number of blocks that executed successfully.
    /// </summary>
    public int BlocksExecuted { get; private set; }

    /// <summary>
    /// Number of blocks that failed.
    /// </summary>
    public int BlocksFailed { get; private set; }

    /// <summary>
    /// Final response from agent blocks (if any).
    /// </summary>
    public string? FinalResponse { get; private set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ExecutionError { get; private set; }

    /// <summary>
    /// Full event stream serialized as JSON array (JSONB).
    /// </summary>
    public string EventsJson { get; private set; } = "[]";

    /// <summary>
    /// Configuration overrides used for this execution (JSONB).
    /// Null for original executions, populated for replays.
    /// </summary>
    public string? ConfigOverridesJson { get; private set; }

    /// <summary>
    /// Parent execution ID for replays (links to original execution).
    /// </summary>
    public Guid? ParentExecutionId { get; private set; }

    /// <summary>
    /// When the execution was started.
    /// </summary>
    public DateTime ExecutedAt { get; private set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Creates a new execution record from a completed pipeline test.
    /// </summary>
    public static RagExecution Create(
        Guid strategyId,
        string pipelineDefinitionJson,
        string testQuery,
        Guid executedByUserId,
        bool success,
        int totalDurationMs,
        int totalTokensUsed,
        decimal totalCost,
        int blocksExecuted,
        int blocksFailed,
        string? finalResponse,
        string? executionError,
        string eventsJson,
        string? configOverridesJson = null,
        Guid? parentExecutionId = null,
        TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        return new RagExecution
        {
            Id = Guid.NewGuid(),
            StrategyId = strategyId,
            PipelineDefinitionJson = pipelineDefinitionJson,
            TestQuery = testQuery,
            ExecutedByUserId = executedByUserId,
            Success = success,
            TotalDurationMs = totalDurationMs,
            TotalTokensUsed = totalTokensUsed,
            TotalCost = totalCost,
            BlocksExecuted = blocksExecuted,
            BlocksFailed = blocksFailed,
            FinalResponse = finalResponse,
            ExecutionError = executionError,
            EventsJson = eventsJson,
            ConfigOverridesJson = configOverridesJson,
            ParentExecutionId = parentExecutionId,
            ExecutedAt = now,
            CreatedAt = now,
            IsDeleted = false
        };
    }

    /// <summary>
    /// Soft deletes the execution record.
    /// </summary>
    public void Delete(TimeProvider? timeProvider = null)
    {
        IsDeleted = true;
        DeletedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }
}
