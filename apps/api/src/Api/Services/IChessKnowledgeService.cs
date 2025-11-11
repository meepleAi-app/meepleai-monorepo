namespace Api.Services;

/// <summary>
/// Service for indexing and managing chess knowledge in the vector database
/// </summary>
public interface IChessKnowledgeService
{
    /// <summary>
    /// Index all chess knowledge into Qdrant
    /// </summary>
    Task<ChessIndexResult> IndexChessKnowledgeAsync(CancellationToken ct = default);

    /// <summary>
    /// Search chess knowledge with specified query
    /// </summary>
    Task<SearchResult> SearchChessKnowledgeAsync(string query, int limit = 5, CancellationToken ct = default);

    /// <summary>
    /// Delete all chess knowledge from the vector database
    /// </summary>
    Task<bool> DeleteChessKnowledgeAsync(CancellationToken ct = default);
}

/// <summary>
/// Result of chess knowledge indexing operation
/// </summary>
public record ChessIndexResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int TotalKnowledgeItems { get; init; }
    public int TotalChunks { get; init; }
    public Dictionary<string, int> CategoryCounts { get; init; } = new();

    public static ChessIndexResult CreateSuccess(int totalItems, int totalChunks, Dictionary<string, int> categoryCounts) =>
        new() { Success = true, TotalKnowledgeItems = totalItems, TotalChunks = totalChunks, CategoryCounts = categoryCounts };

    public static ChessIndexResult CreateFailure(string error) =>
        new() { Success = false, ErrorMessage = error };
}
