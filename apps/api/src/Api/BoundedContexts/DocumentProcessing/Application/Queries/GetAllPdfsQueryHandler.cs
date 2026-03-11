using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetAllPdfsQuery.
/// PDF Storage Management Hub: Enriched with 7-state processing, game joins,
/// chunk counts, file sizes, sorting, and filtering.
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
            .Include(p => p.Game)
            .AsQueryable();

        // Filter by legacy status — map to ProcessingState when possible
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var mappedState = query.Status switch
            {
                "pending" => nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Pending),
                "completed" => nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready),
                "failed" => nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Failed),
                _ => (string?)null
            };

            if (mappedState != null)
            {
                pdfsQuery = pdfsQuery.Where(p => p.ProcessingState == mappedState);
            }
            else
            {
                // "processing" maps to multiple states — filter out terminal states
                var readyState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready);
                var failedState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Failed);
                var pendingState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Pending);
                pdfsQuery = pdfsQuery.Where(p =>
                    p.ProcessingState != readyState &&
                    p.ProcessingState != failedState &&
                    p.ProcessingState != pendingState);
            }
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

        // Filter by game
        if (query.GameId.HasValue)
        {
            pdfsQuery = pdfsQuery.Where(p => p.GameId == query.GameId.Value);
        }

        // Get total count
        var total = await pdfsQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply sorting
        pdfsQuery = ApplySorting(pdfsQuery, query.SortBy, query.SortOrder);

        // Get paginated results with enriched data
        var pdfs = await pdfsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new PdfListItemDto(
                p.Id,
                p.FileName,
                p.Game != null ? p.Game.Name : null,
                p.GameId != Guid.Empty ? p.GameId : null,
                p.ProcessingStatus,
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
