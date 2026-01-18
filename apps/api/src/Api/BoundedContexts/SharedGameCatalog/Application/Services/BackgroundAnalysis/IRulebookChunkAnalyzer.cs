using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Service for analyzing individual rulebook chunks in parallel.
/// Third phase of multi-phase background analysis.
/// </summary>
public interface IRulebookChunkAnalyzer
{
    /// <summary>
    /// Analyzes a single chunk of rulebook content.
    /// </summary>
    /// <param name="chunk">Semantic chunk to analyze</param>
    /// <param name="gameContext">Game context from overview</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result for the chunk</returns>
    Task<ChunkAnalysisResult> AnalyzeChunkAsync(
        SemanticChunk chunk,
        GameContext gameContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes multiple chunks in parallel with concurrency control.
    /// </summary>
    /// <param name="chunks">Chunks to analyze</param>
    /// <param name="gameContext">Game context from overview</param>
    /// <param name="maxParallelism">Max concurrent analyses (default: 3)</param>
    /// <param name="progressCallback">Optional async progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis results for all chunks</returns>
    Task<ParallelAnalysisResult> AnalyzeChunksParallelAsync(
        List<SemanticChunk> chunks,
        GameContext gameContext,
        int maxParallelism = 3,
        Func<int, int, Task>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Game context extracted from overview phase.
/// </summary>
public sealed record GameContext(
    string GameTitle,
    string GameSummary,
    List<string> MainMechanics,
    string VictoryConditionSummary);

/// <summary>
/// Result of analyzing a single chunk.
/// </summary>
public sealed record ChunkAnalysisResult
{
    public int ChunkIndex { get; init; }
    public List<string> ExtractedMechanics { get; init; } = [];
    public List<Resource> Resources { get; init; } = [];
    public List<GamePhase> GamePhases { get; init; } = [];
    public List<string> CommonQuestions { get; init; } = [];
    public string ChunkSummary { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static ChunkAnalysisResult CreateSuccess(
        int chunkIndex,
        List<string> mechanics,
        List<Resource> resources,
        List<GamePhase> phases,
        List<string> questions,
        string summary) => new()
    {
        ChunkIndex = chunkIndex,
        ExtractedMechanics = mechanics,
        Resources = resources,
        GamePhases = phases,
        CommonQuestions = questions,
        ChunkSummary = summary,
        Success = true
    };

    public static ChunkAnalysisResult CreateFailure(int chunkIndex, string error) => new()
    {
        ChunkIndex = chunkIndex,
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Result of parallel chunk analysis.
/// </summary>
public sealed record ParallelAnalysisResult
{
    public List<ChunkAnalysisResult> Results { get; init; } = [];
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double SuccessRate => Results.Count > 0
        ? (double)SuccessCount / Results.Count
        : 0;

    public static ParallelAnalysisResult Create(List<ChunkAnalysisResult> results)
    {
        var successCount = results.Count(r => r.Success);
        return new()
        {
            Results = results,
            SuccessCount = successCount,
            FailureCount = results.Count - successCount
        };
    }
}
