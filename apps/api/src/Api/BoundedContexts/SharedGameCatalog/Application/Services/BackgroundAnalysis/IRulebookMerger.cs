using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Service for merging chunk analyses into final rulebook analysis.
/// Fourth phase of multi-phase background analysis.
/// </summary>
public interface IRulebookMerger
{
    /// <summary>
    /// Merges overview and chunk analyses into cohesive final analysis.
    /// </summary>
    /// <param name="overview">Overview extraction result</param>
    /// <param name="chunkResults">Parallel chunk analysis results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final merged rulebook analysis</returns>
    Task<MergedRulebookAnalysis> MergeAnalysesAsync(
        OverviewExtractionResult overview,
        ParallelAnalysisResult chunkResults,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Final merged rulebook analysis result.
/// </summary>
public sealed record MergedRulebookAnalysis
{
    public string GameTitle { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<string> KeyMechanics { get; init; } = [];
    public VictoryConditions? VictoryConditions { get; init; }
    public List<Resource> Resources { get; init; } = [];
    public List<GamePhase> GamePhases { get; init; } = [];
    public List<string> CommonQuestions { get; init; } = [];
    public decimal ConfidenceScore { get; init; }
    public MergeMetadata Metadata { get; init; } = new();

    public static MergedRulebookAnalysis Create(
        string gameTitle,
        string summary,
        List<string> keyMechanics,
        VictoryConditions? victoryConditions,
        List<Resource> resources,
        List<GamePhase> gamePhases,
        List<string> commonQuestions,
        decimal confidenceScore,
        MergeMetadata metadata) => new()
    {
        GameTitle = gameTitle,
        Summary = summary,
        KeyMechanics = keyMechanics,
        VictoryConditions = victoryConditions,
        Resources = resources,
        GamePhases = gamePhases,
        CommonQuestions = commonQuestions,
        ConfidenceScore = confidenceScore,
        Metadata = metadata
    };
}

/// <summary>
/// Metadata about the merge process.
/// </summary>
public sealed record MergeMetadata
{
    public int TotalChunksProcessed { get; init; }
    public int SuccessfulChunks { get; init; }
    public int FailedChunks { get; init; }
    public double ChunkSuccessRate { get; init; }
    public int DuplicatesRemoved { get; init; }
    public TimeSpan TotalAnalysisTime { get; init; }

    public static MergeMetadata Create(
        int totalChunks,
        int successfulChunks,
        int failedChunks,
        int duplicatesRemoved,
        TimeSpan analysisTime) => new()
    {
        TotalChunksProcessed = totalChunks,
        SuccessfulChunks = successfulChunks,
        FailedChunks = failedChunks,
        ChunkSuccessRate = totalChunks > 0 ? (double)successfulChunks / totalChunks : 0,
        DuplicatesRemoved = duplicatesRemoved,
        TotalAnalysisTime = analysisTime
    };
}
