using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
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

        // Materialize with players+scores so we can compute WinnerPlayerIds and OutcomeType.
        // Single Include chain — no N+1 (EF loads scores in a single subsequent query per page).
        var rawRecords = await dbQuery
            .OrderByDescending(r => r.SessionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = rawRecords
            .Select(r => MapToSummary(r))
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PlayHistoryResponse(records, totalCount, page, pageSize, totalPages);
    }

    /// <summary>
    /// Maps an entity to <see cref="PlayRecordSummaryDto"/>, computing outcome fields via the shared helper.
    /// </summary>
    private static PlayRecordSummaryDto MapToSummary(PlayRecordEntity r) =>
        new(
            r.Id,
            r.GameName,
            r.SessionDate,
            r.Duration,
            (Domain.Enums.PlayRecordStatus)r.Status,
            r.Players.Count,
            r.GameId,
            PlayRecordOutcomeCalculator.WinnerPlayerIds(r.Players),
            PlayRecordOutcomeCalculator.OutcomeType(r.Players)
        );
}
