using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameDocuments;

/// <summary>
/// Handles GetGameDocumentsQuery.
/// Queries VectorDocument entities joined with PdfDocument for metadata,
/// filtered by GameId. Access control is enforced at the endpoint level
/// via RequireSession(), consistent with other KB query handlers.
/// </summary>
internal sealed class GetGameDocumentsHandler : IQueryHandler<GetGameDocumentsQuery, IReadOnlyList<GameDocumentDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetGameDocumentsHandler> _logger;

    public GetGameDocumentsHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetGameDocumentsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<GameDocumentDto>> Handle(
        GetGameDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug(
            "Fetching KB documents for game {GameId} by user {UserId}",
            query.GameId, query.UserId);

        var documents = await (
            from vd in _dbContext.VectorDocuments.AsNoTracking()
            join pdf in _dbContext.PdfDocuments.AsNoTracking()
                on vd.PdfDocumentId equals pdf.Id
            where vd.GameId == query.GameId
            orderby vd.IndexedAt descending
            select new GameDocumentDto(
                vd.Id,
                pdf.FileName,
                MapIndexingStatus(vd.IndexingStatus),
                pdf.PageCount ?? 0,
                vd.IndexedAt ?? pdf.UploadedAt
            )
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Found {Count} KB documents for game {GameId}",
            documents.Count, query.GameId);

        return documents;
    }

    /// <summary>
    /// Maps the persistence IndexingStatus string to the simplified status expected by the frontend.
    /// </summary>
    private static string MapIndexingStatus(string indexingStatus)
    {
        return indexingStatus.ToLowerInvariant() switch
        {
            "completed" => "indexed",
            "pending" or "processing" => "processing",
            "failed" => "failed",
            _ => "processing"
        };
    }
}
