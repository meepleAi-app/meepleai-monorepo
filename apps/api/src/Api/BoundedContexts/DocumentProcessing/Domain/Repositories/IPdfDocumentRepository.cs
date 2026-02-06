using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

internal interface IPdfDocumentRepository : IRepository<PdfDocument, Guid>
{
    Task<IReadOnlyList<PdfDocument>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> FindByPrivateGameIdAsync(Guid privateGameId, CancellationToken cancellationToken = default); // Issue #3664
    Task<IReadOnlyList<PdfDocument>> FindByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfDocument>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default); // Issue #2732: Quota queries
}
