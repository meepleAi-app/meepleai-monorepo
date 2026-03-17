using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetRecentlyProcessedDocuments;

/// <summary>
/// Handler for GetRecentlyProcessedDocumentsQuery.
/// Cross-BC read query joining SharedGameDocuments, PdfDocuments, SharedGames,
/// and ProcessingJobs to build the admin "Recently Processed" widget data.
/// </summary>
internal sealed class GetRecentlyProcessedDocumentsQueryHandler
    : IRequestHandler<GetRecentlyProcessedDocumentsQuery, List<RecentlyProcessedDocumentDto>>
{
    private const int MaxRetries = 3;

    private readonly MeepleAiDbContext _db;

    public GetRecentlyProcessedDocumentsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<List<RecentlyProcessedDocumentDto>> Handle(
        GetRecentlyProcessedDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var limit = Math.Clamp(request.Limit, 1, 50);

        // Cross-BC read query (deliberate — follows GetGameRagReadinessQueryHandler pattern)
        // Join: SharedGameDocuments → PdfDocuments (on PdfDocumentId)
        //       SharedGameDocuments → SharedGames (on SharedGameId)
        //       Left-join ProcessingJobs (on PdfDocumentId) for JobId
        var results = await (
            from sgd in _db.SharedGameDocuments
            join pd in _db.PdfDocuments on sgd.PdfDocumentId equals pd.Id
            join sg in _db.SharedGames on sgd.SharedGameId equals sg.Id
            join pj in _db.ProcessingJobs on pd.Id equals pj.PdfDocumentId into jobs
            from pj in jobs.DefaultIfEmpty()
            where !sg.IsDeleted
            orderby pd.ProcessedAt ?? pd.UploadedAt descending
            select new RecentlyProcessedDocumentDto(
                pd.Id,
                pj != null ? pj.Id : (Guid?)null,
                pd.FileName,
                pd.ProcessingState,
                pd.ProcessedAt ?? pd.UploadedAt,
                pd.ErrorCategory,
                pd.ProcessingState == "Failed" && pd.RetryCount < MaxRetries,
                sg.Id,
                sg.Title,
                sg.ThumbnailUrl)
        )
        .Take(limit)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

        return results;
    }
}
