using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for RulebookAnalysis aggregate.
/// </summary>
public interface IRulebookAnalysisRepository
{
    /// <summary>
    /// Adds a new rulebook analysis to the repository.
    /// </summary>
    Task AddAsync(RulebookAnalysis analysis, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a rulebook analysis by its ID.
    /// </summary>
    Task<RulebookAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active rulebook analysis for a specific game and PDF document.
    /// </summary>
    Task<RulebookAnalysis?> GetActiveAnalysisAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all analyses for a specific shared game.
    /// </summary>
    Task<List<RulebookAnalysis>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all analyses for a specific PDF document.
    /// </summary>
    Task<List<RulebookAnalysis>> GetByPdfDocumentIdAsync(
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing rulebook analysis.
    /// </summary>
    void Update(RulebookAnalysis analysis);

    /// <summary>
    /// Deactivates all analyses for a specific game and PDF document.
    /// Used before activating a new version.
    /// </summary>
    Task DeactivateAllAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the extracted text content from a PDF document.
    /// </summary>
    Task<string> GetPdfTextAsync(Guid pdfDocumentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the document category string for a PDF document.
    /// Issue #5443: Used for pipeline routing gate.
    /// </summary>
    Task<string?> GetPdfDocumentCategoryAsync(Guid pdfDocumentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all SharedGame+PDF pairs where the PDF has extracted text ready for analysis.
    /// </summary>
    Task<List<GamePdfPair>> GetGamePdfPairsWithReadyTextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the set of "SharedGameId:PdfDocumentId" keys that have an active analysis.
    /// </summary>
    Task<HashSet<string>> GetActiveAnalysisKeysAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a SharedGame paired with a PDF document that has extracted text.
/// </summary>
public record GamePdfPair(Guid SharedGameId, string GameTitle, Guid PdfDocumentId);
