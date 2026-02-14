namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Detailed error information for bulk import failures
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
public record BulkImportError
{
    /// <summary>
    /// BGG ID of the game (null if JSON parsing failed before extracting ID)
    /// </summary>
    public int? BggId { get; init; }

    /// <summary>
    /// Name of the game for better error context
    /// </summary>
    public string? GameName { get; init; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Error category: Duplicate | InvalidJson | ApiError | ValidationError
    /// </summary>
    public required string ErrorType { get; init; }
}
