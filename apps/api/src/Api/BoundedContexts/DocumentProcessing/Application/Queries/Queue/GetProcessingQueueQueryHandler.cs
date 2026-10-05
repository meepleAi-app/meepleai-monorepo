using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

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

        // Filter by date range.
        // Issue #4055: fromDate/toDate are bound from the query string as DateTimeOffset, so they
        // carry the client's offset, and they become Npgsql parameters compared against a
        // `timestamptz` column — which Npgsql refuses unless the offset is 0 ("Cannot write
        // DateTimeOffset with Offset=01:00:00 ..."). Measured against the local stack on
        // 2026-10-04: GET /api/v1/admin/queue?fromDate=2026-01-01T00:00:00Z → 200, the same value
        // with +01:00 → 400 `bad_request` whose stack trace is
        // DateTimeOffsetConverter.WriteCore → GetProcessingQueueQueryHandler.Handle. Same for
        // GET /api/v1/admin/kb/processing-queue, which reuses this query.
        // Converting preserves the instant, so the filter semantics do not change.
        if (query.FromDate.HasValue)
        {
            var fromDate = query.FromDate.Value.ToUniversalTime();
            dbQuery = dbQuery.Where(j => j.CreatedAt >= fromDate);
        }
        if (query.ToDate.HasValue)
        {
            var toDate = query.ToDate.Value.ToUniversalTime();
            dbQuery = dbQuery.Where(j => j.CreatedAt <= toDate);
        }

        // Filter by game ID (matches PdfDocument.SharedGameId)
        if (query.GameId.HasValue)
        {
            dbQuery = dbQuery.Where(j => j.PdfDocument.SharedGameId == query.GameId.Value);
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
