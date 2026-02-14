namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Result summary for bulk import from JSON operation
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
public record BulkImportResult
{
    /// <summary>
    /// Total number of games in the JSON input
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Number of games successfully enqueued for import
    /// </summary>
    public int Enqueued { get; set; }

    /// <summary>
    /// Number of games skipped (duplicates already exist)
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Number of games that failed to process
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Detailed error list for skipped and failed games
    /// </summary>
    public List<BulkImportError> Errors { get; set; } = new();
}
