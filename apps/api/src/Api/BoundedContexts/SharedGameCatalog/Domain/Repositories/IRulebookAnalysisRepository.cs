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
}
