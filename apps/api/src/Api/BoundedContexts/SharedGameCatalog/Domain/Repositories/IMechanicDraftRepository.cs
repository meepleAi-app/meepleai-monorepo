using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for MechanicDraft entity.
/// </summary>
public interface IMechanicDraftRepository
{
    /// <summary>
    /// Adds a new mechanic draft.
    /// </summary>
    Task AddAsync(MechanicDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a mechanic draft by its ID.
    /// </summary>
    Task<MechanicDraft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active (non-activated) draft for a specific game and PDF.
    /// Returns the most recently modified draft.
    /// </summary>
    Task<MechanicDraft?> GetDraftForGameAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all drafts for a specific shared game.
    /// </summary>
    Task<List<MechanicDraft>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing mechanic draft.
    /// </summary>
    void Update(MechanicDraft draft);
}
