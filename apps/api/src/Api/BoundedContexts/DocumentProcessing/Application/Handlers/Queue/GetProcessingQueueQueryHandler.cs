using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Handles paginated queue listing with filters.
/// Reads directly from EF entities (CQRS read side).
/// Issue #4731: Queue queries.
/// </summary>
internal class GetProcessingQueueQueryHandler : IQueryHandler<GetProcessingQueueQuery, PaginatedQueueResponse>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetProcessingQueueQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PaginatedQueueResponse> Handle(GetProcessingQueueQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var dbQuery = _dbContext.ProcessingJobs
            .AsNoTracking()
            .Include(j => j.PdfDocument)
            .AsQueryable();

        // Filter by status
        if (!string.IsNullOrWhiteSpace(query.StatusFilter))
        {
            dbQuery = dbQuery.Where(j => j.Status == query.StatusFilter);
        }

        // Filter by date range
        if (query.FromDate.HasValue)
        {
            dbQuery = dbQuery.Where(j => j.CreatedAt >= query.FromDate.Value);
        }
        if (query.ToDate.HasValue)
        {
            dbQuery = dbQuery.Where(j => j.CreatedAt <= query.ToDate.Value);
        }

        // Filter by game ID (matches PdfDocument.GameId or PdfDocument.SharedGameId)
        if (query.GameId.HasValue)
        {
            dbQuery = dbQuery.Where(j =>
                j.PdfDocument.GameId == query.GameId.Value ||
                j.PdfDocument.SharedGameId == query.GameId.Value);
        }

        // Search text (matches PDF filename, case-insensitive via EF.Functions.ILike for PostgreSQL)
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchPattern = $"%{query.SearchText}%";
            dbQuery = dbQuery.Where(j => EF.Functions.ILike(j.PdfDocument.FileName, searchPattern));
        }

        // Total count for pagination
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Sort: active jobs first (Queued/Processing by priority), then completed/failed by date
        var jobs = await dbQuery
            .OrderByDescending(j => j.Status == "Queued" || j.Status == "Processing" ? 1 : 0)
            .ThenBy(j => j.Priority)
            .ThenByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new ProcessingJobDto(
                j.Id,
                j.PdfDocumentId,
                j.PdfDocument.FileName,
                j.UserId,
                j.Status,
                j.Priority,
                j.CurrentStep,
                j.CreatedAt,
                j.StartedAt,
                j.CompletedAt,
                j.ErrorMessage,
                j.RetryCount,
                j.MaxRetries,
                j.Status == "Failed" && j.RetryCount < j.MaxRetries
            ))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        return new PaginatedQueueResponse(
            Jobs: jobs,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages
        );
    }
}
