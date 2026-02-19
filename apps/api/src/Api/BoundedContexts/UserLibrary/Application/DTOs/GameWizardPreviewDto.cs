namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Combined game preview data for the AddGameSheet wizard.
/// Issue #4823: Backend Game Preview API - Unified Wizard Data Endpoint
/// Epic #4817: User Collection Wizard
/// </summary>
internal record GameWizardPreviewDto(
    Guid GameId,
    string Title,
    string? ImageUrl,
    string? ThumbnailUrl,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    decimal? ComplexityRating,
    decimal? AverageRating,
    int? YearPublished,
    string? Description,
    string Source,
    IReadOnlyList<PdfDocumentSummaryDto> Documents,
    int DocumentCount,
    bool IsInUserLibrary,
    Guid? LibraryEntryId,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Mechanics
);

/// <summary>
/// Lightweight PDF document info for wizard preview.
/// Issue #4823: Backend Game Preview API
/// </summary>
internal record PdfDocumentSummaryDto(
    Guid Id,
    string FileName,
    int? PageCount,
    string Status,
    string DocumentType
);
