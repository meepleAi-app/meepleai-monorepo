using Api.Models;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Preview data for game creation from PDF extraction.
/// Includes extracted metadata, BGG match suggestions, and duplicate warnings.
/// Used in wizard UI to show preview before final confirmation.
/// Issue #4138: Backend - Commands and DTOs - PDF Wizard
/// </summary>
public record PdfGamePreviewDto
{
    /// <summary>
    /// Metadata extracted from PDF document
    /// </summary>
    public required GameMetadataDto ExtractedMetadata { get; init; }

    /// <summary>
    /// List of suggested BoardGameGeek matches based on extracted title.
    /// Empty if no matches found or BGG search failed.
    /// </summary>
    public List<BggSearchResultDto> BggSuggestions { get; init; } = [];

    /// <summary>
    /// Whether potential duplicate games were detected in the catalog.
    /// Checks for existing games with similar titles.
    /// </summary>
    public bool HasDuplicateWarning { get; init; }

    /// <summary>
    /// List of existing game titles that might be duplicates.
    /// Shown to user as warning (non-blocking).
    /// </summary>
    public List<string> DuplicateTitles { get; init; } = [];

    /// <summary>
    /// PDF extraction confidence score (0.0 - 1.0).
    /// ≥0.80: High confidence (green)
    /// 0.50-0.79: Medium confidence (yellow)
    /// &lt;0.50: Low confidence (red, requires manual review)
    /// </summary>
    public double ExtractionConfidence { get; init; }

    /// <summary>
    /// Whether the extraction quality meets minimum threshold (≥0.50).
    /// If false, manual review is required before proceeding.
    /// </summary>
    public bool MeetsQualityThreshold { get; init; }
}
