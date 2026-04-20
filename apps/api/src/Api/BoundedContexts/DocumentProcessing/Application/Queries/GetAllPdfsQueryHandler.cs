using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetAllPdfsQuery.
/// PDF Storage Management Hub: Enriched with 7-state processing, game joins,
/// chunk counts, file sizes, sorting, and filtering.
///
/// Task 5 (PDF SharedGameId migration): filters by SharedGameId and uses an
/// explicit LINQ Join to shared_games for GameTitle projection. Status filter
/// maps lowercase API contract values (completed/failed/pending/processing) to
/// the 7-state ProcessingState vocabulary.
/// </summary>
internal sealed class GetAllPdfsQueryHandler : IQueryHandler<GetAllPdfsQuery, PdfListResult>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetAllPdfsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PdfListResult> Handle(GetAllPdfsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pdfsQuery = _dbContext.PdfDocuments
            .AsNoTracking()
            .AsQueryable();

        // Filter by legacy status (API contract uses lowercase) — map to ProcessingState
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            pdfsQuery = query.Status.ToLowerInvariant() switch
            {
                "completed" => pdfsQuery.Where(p => p.ProcessingState == "Ready"),
                "failed" => pdfsQuery.Where(p => p.ProcessingState == "Failed"),
                "pending" => pdfsQuery.Where(p => p.ProcessingState == "Pending"),
                "processing" => pdfsQuery.Where(p =>
                    p.ProcessingState == "Uploading" ||
                    p.ProcessingState == "Extracting" ||
                    p.ProcessingState == "Chunking" ||
                    p.ProcessingState == "Embedding" ||
                    p.ProcessingState == "Indexing"),
                _ => pdfsQuery
            };
        }

        // Filter by 7-state processing state
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            pdfsQuery = pdfsQuery.Where(p => p.ProcessingState == query.State);
        }

        // Filter by file size range
        if (query.MinSizeBytes.HasValue)
        {
            pdfsQuery = pdfsQuery.Where(p => p.FileSizeBytes >= query.MinSizeBytes.Value);
        }

        if (query.MaxSizeBytes.HasValue)
        {
            pdfsQuery = pdfsQuery.Where(p => p.FileSizeBytes <= query.MaxSizeBytes.Value);
        }

        // Filter by upload date range
        if (query.UploadedAfter.HasValue)
        {
            pdfsQuery = pdfsQuery.Where(p => p.UploadedAt >= query.UploadedAfter.Value);
        }

        if (query.UploadedBefore.HasValue)
        {
            pdfsQuery = pdfsQuery.Where(p => p.UploadedAt <= query.UploadedBefore.Value);
        }

        // Filter by shared game (GameId parameter is now semantically SharedGameId)
        if (query.GameId.HasValue)
        {
            pdfsQuery = pdfsQuery.Where(p => p.SharedGameId == query.GameId.Value);
        }

        // Get total count before sorting / paging
        var total = await pdfsQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply sorting
        pdfsQuery = ApplySorting(pdfsQuery, query.SortBy, query.SortOrder);

        // Project via explicit left-join to shared_games so GameTitle can be populated
        // without a correlated subquery. Chunk count still uses correlated subquery
        // since it aggregates the text_chunks child table.
        var pdfs = await (
            from p in pdfsQuery
            join sg in _dbContext.SharedGames on p.SharedGameId equals sg.Id into sgj
            from sg in sgj.DefaultIfEmpty()
            select new PdfListItemDto(
                p.Id,
                p.FileName,
                sg != null ? sg.Title : null,
                p.SharedGameId,
                p.ProcessingState,
                MapStateToProgress(p.ProcessingState),
                p.FileSizeBytes,
                p.PageCount,
                _dbContext.TextChunks.Count(tc => tc.PdfDocumentId == p.Id),
                p.ProcessingError,
                p.ErrorCategory,
                p.RetryCount,
                p.UploadedAt,
                p.ProcessedAt
            ))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PdfListResult(pdfs, total, query.Page, query.PageSize);
    }

    private static IQueryable<PdfDocumentEntity> ApplySorting(
        IQueryable<PdfDocumentEntity> query, string? sortBy, string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy?.ToLowerInvariant() switch
        {
            "filename" => descending
                ? query.OrderByDescending(p => p.FileName)
                : query.OrderBy(p => p.FileName),
            "filesize" => descending
                ? query.OrderByDescending(p => p.FileSizeBytes)
                : query.OrderBy(p => p.FileSizeBytes),
            "state" => descending
                ? query.OrderByDescending(p => p.ProcessingState)
                : query.OrderBy(p => p.ProcessingState),
            "uploadedat" => descending
                ? query.OrderByDescending(p => p.UploadedAt)
                : query.OrderBy(p => p.UploadedAt),
            _ => query.OrderByDescending(p => p.UploadedAt)
        };
    }

    private static int MapStateToProgress(string processingState)
    {
        return processingState switch
        {
            "Pending" => 0,
            "Uploading" => 10,
            "Extracting" => 30,
            "Chunking" => 50,
            "Embedding" => 70,
            "Indexing" => 85,
            "Ready" => 100,
            "Failed" => 0,
            _ => 0
        };
    }
}
