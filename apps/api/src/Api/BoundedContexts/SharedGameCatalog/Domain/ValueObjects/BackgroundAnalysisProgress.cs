namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing progress of background rulebook analysis.
/// </summary>
public sealed record BackgroundAnalysisProgress
{
    public AnalysisPhase CurrentPhase { get; init; }
    public int PercentageComplete { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    private BackgroundAnalysisProgress() { }

    public static BackgroundAnalysisProgress Create(
        AnalysisPhase currentPhase,
        int percentageComplete,
        string statusMessage,
        TimeSpan? estimatedTimeRemaining = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percentageComplete);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentageComplete, 100);

        return new BackgroundAnalysisProgress
        {
            CurrentPhase = currentPhase,
            PercentageComplete = percentageComplete,
            StatusMessage = statusMessage,
            EstimatedTimeRemaining = estimatedTimeRemaining
        };
    }

    public static BackgroundAnalysisProgress ForPhaseStart(AnalysisPhase phase) =>
        Create(phase, phase.GetBaseProgress(), phase.GetDisplayName());

    public static BackgroundAnalysisProgress ForPhaseProgress(
        AnalysisPhase phase,
        double phaseProgress,
        int totalItems,
        int processedItems)
    {
        var baseProgress = phase.GetBaseProgress();
        var weight = phase.GetProgressWeight();
        var percentage = baseProgress + (int)(weight * phaseProgress);

        var message = phase == AnalysisPhase.ChunkAnalysis
            ? $"Analyzing chunks ({processedItems}/{totalItems})"
            : phase.GetDisplayName();

        TimeSpan? estimatedTime = null;
        if (phase == AnalysisPhase.ChunkAnalysis && processedItems > 0 && phaseProgress > 0)
        {
            var avgTimePerChunk = TimeSpan.FromSeconds(30);
            var remainingChunks = totalItems - processedItems;
            estimatedTime = avgTimePerChunk * remainingChunks;
        }

        return Create(phase, percentage, message, estimatedTime);
    }

    public static BackgroundAnalysisProgress Completed() =>
        Create(AnalysisPhase.MergeAndValidation, 100, "Completed");
}
