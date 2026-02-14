namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Result of creating a SharedGame from PDF-extracted metadata.
/// Contains the created game ID, approval status, and quality metrics.
/// Issue #4138: Backend - Commands and DTOs - PDF Wizard
/// </summary>
public record CreateGameFromPdfResult
{
    /// <summary>
    /// ID of the created SharedGame entity
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Current approval status of the created game.
    /// "Published" if auto-approved (Admin user or RequiresApproval=false)
    /// "PendingApproval" if approval request was created (Editor user with RequiresApproval=true)
    /// "Draft" if manually set to draft status
    /// </summary>
    public required string ApprovalStatus { get; init; }

    /// <summary>
    /// PDF extraction quality score (0.0 - 1.0).
    /// Reflects confidence of AI metadata extraction from PDF.
    /// </summary>
    public required double QualityScore { get; init; }

    /// <summary>
    /// Whether duplicate games with similar titles were detected during creation.
    /// Non-blocking warning for user awareness.
    /// </summary>
    public bool DuplicateWarning { get; init; }

    /// <summary>
    /// List of potentially duplicate game titles found in catalog
    /// </summary>
    public List<string> DuplicateTitles { get; init; } = [];

    /// <summary>
    /// Whether BGG enrichment was applied successfully
    /// </summary>
    public bool BggEnrichmentApplied { get; init; }

    /// <summary>
    /// BGG ID that was used for enrichment (null if no enrichment)
    /// </summary>
    public int? EnrichedWithBggId { get; init; }
}
