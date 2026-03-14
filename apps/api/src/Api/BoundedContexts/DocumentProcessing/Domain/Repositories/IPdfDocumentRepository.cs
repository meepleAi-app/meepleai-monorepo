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
}
