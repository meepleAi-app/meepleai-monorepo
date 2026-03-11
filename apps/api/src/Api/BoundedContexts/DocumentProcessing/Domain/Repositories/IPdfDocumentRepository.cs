using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

internal interface IPdfDocumentRepository : IRepository<PdfDocument, Guid>
{
    Task<IReadOnlyList<PdfDocument>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> FindByPrivateGameIdAsync(Guid privateGameId, CancellationToken cancellationToken = default); // Issue #3664
#pragma warning disable S1133 // Deprecated code kept for backward compatibility during migration (Issue #96)
    [Obsolete("Use FindByStateAsync(PdfProcessingState) instead. This method queries the deprecated ProcessingStatus column.")]
    Task<IReadOnlyList<PdfDocument>> FindByStatusAsync(string status, CancellationToken cancellationToken = default);
#pragma warning restore S1133
    Task<IReadOnlyList<PdfDocument>> FindByStateAsync(PdfProcessingState state, CancellationToken cancellationToken = default); // Issue #96
    Task<IReadOnlyList<PdfDocument>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default); // Issue #2732: Quota queries
    Task<bool> ExistsByContentHashAsync(string contentHash, Guid? gameId, Guid? privateGameId, CancellationToken cancellationToken = default);
}
