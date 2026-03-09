namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Analysis completion status reflecting critical section coverage.
/// Issue #5452: Critical section quality gate.
/// </summary>
public enum AnalysisCompletionStatus
{
    /// <summary>All critical sections successfully analyzed.</summary>
    Complete,

    /// <summary>Some critical sections failed; analysis is usable but flagged.</summary>
    PartiallyComplete,

    /// <summary>Analysis failed entirely.</summary>
    Failed
}

/// <summary>
/// Classifies semantic chunks as critical or non-critical based on section content.
/// Critical sections: Victory Conditions, Setup, Turn Structure.
/// Issue #5452: Critical section quality gate.
/// </summary>
public static class CriticalSectionClassifier
{
    private static readonly Dictionary<CriticalSectionType, string[]> CriticalKeywords = new()
    {
        [CriticalSectionType.VictoryConditions] = new[]
        {
            "victory", "winning", "win condition", "end game", "endgame",
            "game over", "game end", "scoring", "final scoring", "how to win"
        },
        [CriticalSectionType.Setup] = new[]
        {
            "setup", "set up", "preparation", "getting started", "before you begin",
            "game setup", "initial setup", "starting position", "components"
        },
        [CriticalSectionType.TurnStructure] = new[]
        {
            "turn structure", "turn order", "turn sequence", "player turn",
            "round structure", "phase", "action phase", "gameplay", "playing the game",
            "on your turn", "during your turn"
        }
    };

    /// <summary>
    /// Classifies a chunk based on its section header and content.
    /// Returns the critical section type if detected, null otherwise.
    /// </summary>
    public static CriticalSectionType? Classify(string? sectionHeader, string content)
    {
        var textToCheck = ((sectionHeader ?? "") + " " + GetContentSample(content)).ToLowerInvariant();

        foreach (var (sectionType, keywords) in CriticalKeywords)
        {
            if (keywords.Any(keyword => textToCheck.Contains(keyword, StringComparison.Ordinal)))
            {
                return sectionType;
            }
        }

        return null;
    }

    /// <summary>
    /// Evaluates chunk analysis results and determines completion status.
    /// Returns (status, missingSections).
    /// </summary>
    public static (AnalysisCompletionStatus Status, List<string> MissingSections) EvaluateResults(
        IReadOnlyList<SemanticChunk> allChunks,
        IReadOnlyList<int> failedChunkIndices)
    {
        var failedSet = new HashSet<int>(failedChunkIndices);

        var failedCriticalSections = allChunks
            .Where(c => c.IsCriticalSection && failedSet.Contains(c.ChunkIndex))
            .Select(c => c.CriticalSectionType?.ToString() ?? "Unknown")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (failedCriticalSections.Count > 0)
        {
            return (AnalysisCompletionStatus.PartiallyComplete, failedCriticalSections);
        }

        // Check non-critical success rate (75% threshold)
        var nonCriticalChunks = allChunks.Where(c => !c.IsCriticalSection).ToList();
        if (nonCriticalChunks.Count > 0)
        {
            var failedNonCritical = nonCriticalChunks.Count(c => failedSet.Contains(c.ChunkIndex));
            var successRate = 1.0 - ((double)failedNonCritical / nonCriticalChunks.Count);

            if (successRate < 0.75)
            {
                return (AnalysisCompletionStatus.Failed,
                    new List<string> { $"Non-critical section success rate {successRate:P0} below 75% threshold" });
            }
        }

        return (AnalysisCompletionStatus.Complete, new List<string>());
    }

    private static string GetContentSample(string content)
    {
        // Only check first 500 chars for performance
        return content.Length > 500 ? content[..500] : content;
    }
}
