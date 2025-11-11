namespace Api.Models;

/// <summary>
/// Request to export multiple rule specs as a ZIP archive
/// </summary>
public record BulkExportRequest
{
    /// <summary>
    /// List of rule spec IDs to export (gameId format)
    /// </summary>
    public required List<string> RuleSpecIds { get; init; }
}

/// <summary>
/// Request to delete multiple rule specs
/// </summary>
public record BulkDeleteRequest
{
    /// <summary>
    /// List of rule spec IDs to delete (gameId format)
    /// </summary>
    public required List<string> RuleSpecIds { get; init; }
}

/// <summary>
/// Request to duplicate multiple rule specs
/// </summary>
public record BulkDuplicateRequest
{
    /// <summary>
    /// List of rule spec IDs to duplicate (gameId format)
    /// </summary>
    public required List<string> RuleSpecIds { get; init; }
}

/// <summary>
/// Result of a bulk import operation
/// </summary>
public record BulkImportResult
{
    /// <summary>
    /// Number of rule specs successfully imported
    /// </summary>
    public required int Imported { get; init; }

    /// <summary>
    /// Number of rule specs that failed to import
    /// </summary>
    public required int Failed { get; init; }

    /// <summary>
    /// List of import errors with details
    /// </summary>
    public required List<ImportError> Errors { get; init; }
}

/// <summary>
/// Details about an import error
/// </summary>
public record ImportError
{
    /// <summary>
    /// Name of the file that failed to import
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Line number where the error occurred (if applicable)
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Error message describing what went wrong
    /// </summary>
    public required string Error { get; init; }
}

/// <summary>
/// Result of a bulk delete operation
/// </summary>
public record BulkDeleteResult
{
    /// <summary>
    /// Number of rule specs successfully deleted
    /// </summary>
    public required int Deleted { get; init; }
}

/// <summary>
/// Result of a bulk duplicate operation
/// </summary>
public record BulkDuplicateResult
{
    /// <summary>
    /// Number of rule specs successfully duplicated
    /// </summary>
    public required int Duplicated { get; init; }

    /// <summary>
    /// List of newly created rule spec IDs
    /// </summary>
    public required List<string> NewRuleSpecIds { get; init; }
}
