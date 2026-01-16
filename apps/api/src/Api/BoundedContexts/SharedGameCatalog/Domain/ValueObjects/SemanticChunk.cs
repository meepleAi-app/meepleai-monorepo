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

    private SemanticChunk() { }

    public static SemanticChunk Create(
        int chunkIndex,
        string content,
        int startCharIndex,
        int endCharIndex,
        string? sectionHeader = null,
        List<string>? contextHeaders = null)
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
            ContextHeaders = contextHeaders ?? []
        };
    }

    public int CharacterCount => Content.Length;
}
