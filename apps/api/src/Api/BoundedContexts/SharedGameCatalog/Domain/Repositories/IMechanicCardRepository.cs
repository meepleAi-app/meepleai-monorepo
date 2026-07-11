using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository for the <see cref="MechanicCard"/> aggregate (#527). All mutations are staged into the
/// shared <c>DbContext</c> and committed by <c>IUnitOfWork.SaveChangesAsync</c> so the card, its audit
/// row, and the analysis FK update commit atomically.
/// </summary>
public interface IMechanicCardRepository
{
    /// <summary>Stages a new card for insertion and collects its domain events for post-save dispatch.</summary>
    Task AddAsync(MechanicCard card, CancellationToken cancellationToken = default);

    /// <summary>Stages an append-only audit-log row (same transaction as the card mutation).</summary>
    void AddAuditLog(MechanicCardAuditLog entry);

    /// <summary>
    /// Highest existing <c>version</c> for the game across ALL cards (suppressed or not) — the basis
    /// for the monotonic-per-game version (AD-3). Returns 0 when the game has no cards yet.
    /// </summary>
    Task<int> GetMaxVersionForGameAsync(Guid sharedGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The active (non-suppressed) card for the game, if any — used by the publish race pre-check (F3).
    /// Honors the suppression query filter, so a suppressed card returns null.
    /// </summary>
    Task<MechanicCard?> GetActiveByGameAsync(Guid sharedGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a card by id ignoring the suppression filter — used to validate <c>previousCardId</c> on
    /// republish (AC-5), which must reference a suppressed card.
    /// </summary>
    Task<MechanicCard?> GetByIdIgnoringFiltersAsync(Guid cardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-time context for the public card's APA "copy citation" output (#530): the game's
    /// publication year and the source PDF's display name. Both fields may be null when unknown.
    /// </summary>
    Task<MechanicCardApaContext> GetApaContextAsync(
        Guid sharedGameId, Guid pdfDocumentId, CancellationToken cancellationToken = default);
}

/// <summary>Read-time enrichment for the APA citation format (#530). Fields nullable when unknown.</summary>
public sealed record MechanicCardApaContext(int? PublicationYear, string? DocumentName);
