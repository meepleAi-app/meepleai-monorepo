namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Orchestrator for multi-phase background rulebook analysis.
/// Coordinates 4 phases: Overview → Chunking → Parallel Analysis → Merge.
/// </summary>
public interface IBackgroundRulebookAnalysisOrchestrator
{
    /// <summary>
    /// Executes complete 4-phase background analysis for large rulebooks.
    /// </summary>
    /// <param name="taskId">Unique task identifier</param>
    /// <param name="sharedGameId">Game ID</param>
    /// <param name="pdfDocumentId">PDF document ID</param>
    /// <param name="gameName">Game name</param>
    /// <param name="rulebookContent">Full rulebook content</param>
    /// <param name="createdBy">User ID initiating analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final merged analysis or error</returns>
    Task<OrchestrationResult> ExecuteBackgroundAnalysisAsync(
        string taskId,
        Guid sharedGameId,
        Guid pdfDocumentId,
        string gameName,
        string rulebookContent,
        Guid createdBy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of orchestrated background analysis.
/// </summary>
public sealed record OrchestrationResult
{
    public bool Success { get; init; }
    public MergedRulebookAnalysis? Analysis { get; init; }
    public string? ErrorMessage { get; init; }
    public OrchestrationMetrics Metrics { get; init; } = new();

    public static OrchestrationResult CreateSuccess(
        MergedRulebookAnalysis analysis,
        OrchestrationMetrics metrics) => new()
        {
            Success = true,
            Analysis = analysis,
            Metrics = metrics
        };

    public static OrchestrationResult CreateFailure(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Metrics collected during orchestration.
/// </summary>
public sealed record OrchestrationMetrics
{
    public TimeSpan Phase1Duration { get; init; }
    public TimeSpan Phase2Duration { get; init; }
    public TimeSpan Phase3Duration { get; init; }
    public TimeSpan Phase4Duration { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int TotalChunks { get; init; }
    public int ChunksAnalyzed { get; init; }
    public int ChunksFailed { get; init; }
}
