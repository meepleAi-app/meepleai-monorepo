namespace Api.Services;

/// <summary>
/// Document chunk with embedding.
/// Extracted from QdrantService.cs — used across multiple bounded contexts.
/// </summary>
internal record DocumentChunk
{
    public string Text { get; init; } = string.Empty;
    public float[] Embedding { get; init; } = Array.Empty<float>();
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
}

/// <summary>
/// Result of indexing operation.
/// </summary>
internal record IndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int IndexedCount { get; init; }
    public static IndexResult CreateSuccess(int count) =>
        new() { Success = true, IndexedCount = count };
    public static IndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of search operation (legacy Qdrant-style).
/// </summary>
internal record SearchResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SearchResultItem> Results { get; init; } = new();
    public static SearchResult CreateSuccess(List<SearchResultItem> results) =>
        new() { Success = true, Results = results };
    public static SearchResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Single search result item.
/// </summary>
internal record SearchResultItem
{
    public float Score { get; init; }
    public string Text { get; init; } = string.Empty;
    public string PdfId { get; init; } = string.Empty;
    public int Page { get; init; }
    public int ChunkIndex { get; init; }
}

/// <summary>
/// Result of chess knowledge indexing operation.
/// </summary>
internal record ChessIndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalKnowledgeItems { get; init; }
    public int TotalChunks { get; init; }
    public Dictionary<string, int> CategoryCounts { get; init; } = new(StringComparer.Ordinal);

    public static ChessIndexResult CreateSuccess(int totalItems, int totalChunks, Dictionary<string, int> categoryCounts) =>
        new() { Success = true, TotalKnowledgeItems = totalItems, TotalChunks = totalChunks, CategoryCounts = categoryCounts };

    public static ChessIndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
