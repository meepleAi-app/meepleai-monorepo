using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetSharedGameKbCardsQuery.
/// Joins vector_documents + pdf_documents + shared_game_documents for a shared game.
/// Issue #4925
/// </summary>
internal sealed class GetSharedGameKbCardsQueryHandler
    : IRequestHandler<GetSharedGameKbCardsQuery, IReadOnlyList<KbCardDto>>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetSharedGameKbCardsQueryHandler> _logger;

    public GetSharedGameKbCardsQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetSharedGameKbCardsQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<KbCardDto>> Handle(
        GetSharedGameKbCardsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "GetSharedGameKbCards: sharedGameId={SharedGameId}, status={Status}",
            request.SharedGameId, request.Status ?? "all");

        var query = from vd in _db.VectorDocuments.AsNoTracking()
                    where vd.SharedGameId == request.SharedGameId
                    join pdf in _db.PdfDocuments.AsNoTracking()
                        on vd.PdfDocumentId equals pdf.Id
                    join sgd in _db.SharedGameDocuments.AsNoTracking()
                        on vd.PdfDocumentId equals sgd.PdfDocumentId into sgdGroup
                    from sgd in sgdGroup.DefaultIfEmpty()
                    select new KbCardDto(
                        vd.Id,
                        vd.PdfDocumentId,
                        pdf.FileName,
                        vd.IndexingStatus,
                        vd.ChunkCount,
                        vd.IndexedAt,
                        sgd != null ? ((SharedGameDocumentType)sgd.DocumentType).ToString() : null,
                        sgd != null ? sgd.Version : null,
                        sgd != null && sgd.IsActive
                    );

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusFilter = request.Status.ToLowerInvariant();
            query = query.Where(x => x.IndexingStatus == statusFilter);
        }

        var results = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "GetSharedGameKbCards: found {Count} cards for shared game {SharedGameId}",
            results.Count, request.SharedGameId);

        return results.AsReadOnly();
    }
}
