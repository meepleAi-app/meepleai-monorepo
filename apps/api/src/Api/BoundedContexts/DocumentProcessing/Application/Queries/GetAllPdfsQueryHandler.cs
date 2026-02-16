using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Handler for GetAllPdfsQuery - simplified version without complex joins.
/// Returns all PDFs with basic processing status for admin monitoring.
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

        // Base query
        var pdfsQuery = _dbContext.PdfDocuments.AsNoTracking();

        // Filter by status
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            pdfsQuery = pdfsQuery.Where(p => p.ProcessingStatus == query.Status);
        }

        // Get total
        var total = await pdfsQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Get paginated results - use positional args for EF compatibility
        var pdfs = await pdfsQuery
            .OrderByDescending(p => p.UploadedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new PdfListItemDto(
                p.Id,
                p.FileName,
                null, // GameTitle - client fetches separately
                p.GameId != Guid.Empty ? p.GameId : null,
                p.ProcessingStatus,
                p.PageCount,
                0, // ChunkCount - show in detail view
                p.ProcessingError,
                p.UploadedAt,
                p.ProcessedAt
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PdfListResult(pdfs, total, query.Page, query.PageSize);
    }
}
