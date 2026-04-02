using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;

/// <summary>
/// Handles GetAdminGameKbDocumentsQuery.
/// Returns all vector documents indexed for a specific game.
/// KB-01: Admin per-game KB backend.
/// </summary>
internal sealed class GetAdminGameKbDocumentsQueryHandler
    : IQueryHandler<GetAdminGameKbDocumentsQuery, AdminGameKbDocumentsDto>
{
    private readonly IVectorDocumentRepository _repo;
    private readonly ILogger<GetAdminGameKbDocumentsQueryHandler> _logger;

    public GetAdminGameKbDocumentsQueryHandler(
        IVectorDocumentRepository repo,
        ILogger<GetAdminGameKbDocumentsQueryHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminGameKbDocumentsDto> Handle(
        GetAdminGameKbDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug("Fetching KB documents for game {GameId}", query.GameId);

        var documents = await _repo.GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var items = documents
            .Select(d => new AdminKbDocumentItemDto(
                Id: d.Id,
                PdfDocumentId: d.PdfDocumentId,
                Language: d.Language,
                ChunkCount: d.TotalChunks,
                IndexedAt: d.IndexedAt,
                SharedGameId: d.SharedGameId))
            .ToList();

        return new AdminGameKbDocumentsDto(query.GameId, items);
    }
}
