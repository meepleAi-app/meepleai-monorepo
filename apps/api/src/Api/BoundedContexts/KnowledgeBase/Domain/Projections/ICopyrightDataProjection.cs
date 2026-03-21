namespace Api.BoundedContexts.KnowledgeBase.Domain.Projections;

/// <summary>
/// Read-only projection for copyright tier resolution.
/// Phase 6 pattern: cross-BC data access without coupling to internal repositories.
/// Queries DocumentProcessing (PdfDocuments) and UserLibrary (UserLibraryEntries) tables.
/// </summary>
public interface ICopyrightDataProjection
{
    /// <summary>
    /// Retrieves PDF copyright metadata for a batch of document IDs.
    /// Returns only documents that exist; missing IDs are omitted from the result.
    /// </summary>
    /// <param name="documentIds">String-encoded document GUIDs (from vector store metadata).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary keyed by document ID string with copyright info values.</returns>
    Task<IReadOnlyDictionary<string, PdfCopyrightInfo>> GetPdfCopyrightInfoAsync(
        IReadOnlyList<string> documentIds,
        CancellationToken ct);

    /// <summary>
    /// Checks whether a user owns any of the specified games (via UserLibrary ownership declaration).
    /// Anonymous users (Guid.Empty) always receive false for all game IDs.
    /// </summary>
    /// <param name="userId">The user to check ownership for.</param>
    /// <param name="gameIds">Game IDs to check (SharedGame or PrivateGame).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary keyed by game ID with ownership boolean values.</returns>
    Task<IReadOnlyDictionary<Guid, bool>> CheckOwnershipAsync(
        Guid userId,
        IReadOnlyList<Guid> gameIds,
        CancellationToken ct);
}
