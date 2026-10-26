using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

internal interface IPdfDocumentRepository : IRepository<PdfDocument, Guid>
{
    Task<IReadOnlyList<PdfDocument>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> FindByPrivateGameIdAsync(Guid privateGameId, CancellationToken cancellationToken = default); // Issue #3664
    Task<IReadOnlyList<PdfDocument>> FindByStateAsync(PdfProcessingState state, CancellationToken cancellationToken = default); // Issue #96
    Task<IReadOnlyList<PdfDocument>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default); // Issue #2732: Quota queries
    Task<bool> ExistsByContentHashAsync(string contentHash, Guid? gameId, Guid? privateGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a PDF that was uploaded against a catalog game (<c>SharedGameId</c> set) by content hash, across users.
    /// The catalog deduplication looks only here (#4326): a private PDF is not a catalog document, and reusing it left
    /// the catalog game without one.
    /// </summary>
    Task<PdfDocument?> FindCatalogByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the PDF that a user already uploaded to ONE private game, by content hash. Private deduplication reuses
    /// nothing else (#4317, "strada 1"): a private game has its own PDF and its own index, even for the same content.
    /// </summary>
    Task<PdfDocument?> FindByContentHashForPrivateGameAsync(
        string contentHash, Guid userId, Guid privateGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a PDF by content hash scoped to a single user, whatever game it was uploaded to. Used when the upload
    /// names no game at all; an upload for a catalog game or a private game has its own lookup.
    /// </summary>
    Task<PdfDocument?> FindByContentHashForUserAsync(string contentHash, Guid userId, CancellationToken cancellationToken = default);
}
