namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents a phase in the multi-phase rulebook analysis process.
/// </summary>
public enum AnalysisPhase
{
    /// <summary>Phase 1: Overview extraction (10% progress)</summary>
    OverviewExtraction = 1,

    /// <summary>Phase 2: Semantic chunking (20% progress)</summary>
    SemanticChunking = 2,

    /// <summary>Phase 3: Chunk-by-chunk analysis (60% progress)</summary>
    ChunkAnalysis = 3,

    /// <summary>Phase 4: Merge and validation (10% progress)</summary>
    MergeAndValidation = 4
}

/// <summary>
/// Extension methods for AnalysisPhase.
/// </summary>
public static class AnalysisPhaseExtensions
{
    private static readonly Dictionary<AnalysisPhase, string> PhaseNames = new()
    {
        [AnalysisPhase.OverviewExtraction] = "Overview extraction",
        [AnalysisPhase.SemanticChunking] = "Semantic chunking",
        [AnalysisPhase.ChunkAnalysis] = "Analyzing chunks",
        [AnalysisPhase.MergeAndValidation] = "Merging results"
    };

    private static readonly Dictionary<AnalysisPhase, int> PhaseBaseProgress = new()
    {
        [AnalysisPhase.OverviewExtraction] = 0,
        [AnalysisPhase.SemanticChunking] = 10,
        [AnalysisPhase.ChunkAnalysis] = 20,
        [AnalysisPhase.MergeAndValidation] = 80
    };

    public static string GetDisplayName(this AnalysisPhase phase) =>
        PhaseNames.GetValueOrDefault(phase, "Unknown");

    public static int GetBaseProgress(this AnalysisPhase phase) =>
        PhaseBaseProgress.GetValueOrDefault(phase, 0);

    public static int GetProgressWeight(this AnalysisPhase phase) => phase switch
    {
        AnalysisPhase.OverviewExtraction => 10,
        AnalysisPhase.SemanticChunking => 10,
        AnalysisPhase.ChunkAnalysis => 60,
        AnalysisPhase.MergeAndValidation => 20,
        _ => 0
    };
}
