using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Handles retrieving paginated play history for a user.
/// Issue #3890: CQRS queries for play records.
/// </summary>
internal class GetUserPlayHistoryQueryHandler : IQueryHandler<GetUserPlayHistoryQuery, PlayHistoryResponse>
{
    private readonly MeepleAiDbContext _context;

    public GetUserPlayHistoryQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PlayHistoryResponse> Handle(
        GetUserPlayHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Validate pagination
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var dbQuery = _context.PlayRecords
            .AsNoTracking()
            .Where(r => r.CreatedByUserId == query.UserId);

        // Apply game filter if provided
        if (query.GameId.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.GameId == query.GameId.Value);
        }

        // Get total count
        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply pagination and projection
        var records = await dbQuery
            .OrderByDescending(r => r.SessionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PlayRecordSummaryDto(
                r.Id,
                r.GameName,
                r.SessionDate,
                r.Duration,
                (Domain.Enums.PlayRecordStatus)r.Status,
                r.Players.Count
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PlayHistoryResponse(records, totalCount, page, pageSize, totalPages);
    }
}
