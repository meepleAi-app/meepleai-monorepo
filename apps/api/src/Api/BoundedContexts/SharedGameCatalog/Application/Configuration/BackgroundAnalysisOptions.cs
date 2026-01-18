namespace Api.BoundedContexts.SharedGameCatalog.Application.Configuration;

/// <summary>
/// Configuration options for background rulebook analysis.
/// Issue #2454: Background Processing for Large Rulebooks
/// </summary>
public sealed class BackgroundAnalysisOptions
{
    public const string SectionName = "Features:RulebookAnalysis:BackgroundProcessing";

    /// <summary>
    /// Character threshold for triggering background processing.
    /// Rulebooks above this size are processed asynchronously.
    /// Default: 30000 chars (~50 pages)
    /// </summary>
    public int LargeRulebookThreshold { get; init; } = 30000;

    /// <summary>
    /// Maximum chunk size for semantic chunking.
    /// Default: 10000 chars
    /// </summary>
    public int MaxChunkSize { get; init; } = 10000;

    /// <summary>
    /// Overlap size between chunks for context preservation.
    /// Default: 500 chars
    /// </summary>
    public int OverlapSize { get; init; } = 500;

    /// <summary>
    /// Cosine similarity threshold for semantic boundary detection.
    /// Lower values create more chunks. Range: 0.0-1.0
    /// Default: 0.75
    /// </summary>
    public double SemanticSimilarityThreshold { get; init; } = 0.75;

    /// <summary>
    /// Maximum concurrent chunk analyses.
    /// Default: 3
    /// </summary>
    public int MaxParallelChunks { get; init; } = 3;

    /// <summary>
    /// Maximum retries per phase on transient failures.
    /// Default: 3
    /// </summary>
    public int MaxPhaseRetries { get; init; } = 3;

    /// <summary>
    /// Initial retry delay with exponential backoff.
    /// Default: 1 second
    /// </summary>
    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum timeout for background task execution.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan MaxTaskTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// TTL for progress tracking in Redis.
    /// Default: 24 hours
    /// </summary>
    public TimeSpan ProgressCacheDuration { get; init; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Minimum chunk success rate to proceed with merge.
    /// Default: 0.7 (70%)
    /// </summary>
    public double MinimumChunkSuccessRate { get; init; } = 0.7;

    /// <summary>
    /// Minimum candidate section size for semantic chunking.
    /// Default: 100 chars
    /// </summary>
    public int MinimumSectionSize { get; init; } = 100;

    /// <summary>
    /// Overview sampling: beginning chars.
    /// Default: 10000 chars
    /// </summary>
    public int OverviewSampleBeginning { get; init; } = 10000;

    /// <summary>
    /// Overview sampling: middle section chars.
    /// Default: 5000 chars
    /// </summary>
    public int OverviewSampleMiddle { get; init; } = 5000;

    /// <summary>
    /// Overview sampling: end chars.
    /// Default: 2000 chars
    /// </summary>
    public int OverviewSampleEnd { get; init; } = 2000;

    /// <summary>
    /// Progress percentage allocated to Phase 1 (Overview Extraction).
    /// Default: 10%
    /// </summary>
    public int Phase1ProgressWeight { get; init; } = 10;

    /// <summary>
    /// Progress percentage allocated to Phase 2 (Semantic Chunking).
    /// Default: 10% (cumulative: 20%)
    /// </summary>
    public int Phase2ProgressWeight { get; init; } = 10;

    /// <summary>
    /// Progress percentage allocated to Phase 3 (Chunk Analysis).
    /// Default: 60% (cumulative: 80%)
    /// </summary>
    public int Phase3ProgressWeight { get; init; } = 60;

    /// <summary>
    /// Progress percentage allocated to Phase 4 (Merge and Validation).
    /// Default: 20% (cumulative: 100%)
    /// </summary>
    public int Phase4ProgressWeight { get; init; } = 20;
}
