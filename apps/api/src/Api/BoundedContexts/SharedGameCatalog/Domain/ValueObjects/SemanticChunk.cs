namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents a semantically coherent chunk of rulebook content for analysis.
/// </summary>
public sealed record SemanticChunk
{
    public int ChunkIndex { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? SectionHeader { get; init; }
    public int StartCharIndex { get; init; }
    public int EndCharIndex { get; init; }
    public List<string> ContextHeaders { get; init; } = [];

    /// <summary>
    /// Whether this chunk contains a critical section (Victory Conditions, Setup, Turn Structure).
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public bool IsCriticalSection { get; init; }

    /// <summary>
    /// The critical section type if this is a critical chunk.
    /// Issue #5452: Critical section quality gate.
    /// </summary>
    public CriticalSectionType? CriticalSectionType { get; init; }

    private SemanticChunk() { }

    public static SemanticChunk Create(
        int chunkIndex,
        string content,
        int startCharIndex,
        int endCharIndex,
        string? sectionHeader = null,
        List<string>? contextHeaders = null,
        bool isCriticalSection = false,
        CriticalSectionType? criticalSectionType = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunkIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentOutOfRangeException.ThrowIfNegative(startCharIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(endCharIndex, startCharIndex);

        return new SemanticChunk
        {
            ChunkIndex = chunkIndex,
            Content = content,
            SectionHeader = sectionHeader,
            StartCharIndex = startCharIndex,
            EndCharIndex = endCharIndex,
            ContextHeaders = contextHeaders ?? [],
            IsCriticalSection = isCriticalSection,
            CriticalSectionType = criticalSectionType
        };
    }

    public int CharacterCount => Content.Length;
}

/// <summary>
/// Types of critical sections in rulebook analysis.
/// Issue #5452: Critical section quality gate.
/// </summary>
public enum CriticalSectionType
{
    VictoryConditions,
    Setup,
    TurnStructure
}
