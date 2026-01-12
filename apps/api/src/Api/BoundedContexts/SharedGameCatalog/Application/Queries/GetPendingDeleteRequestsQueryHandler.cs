using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting pending delete requests for shared games.
/// Admins use this to review and approve/reject deletion requests from editors.
/// </summary>
internal sealed class GetPendingDeleteRequestsQueryHandler : IRequestHandler<GetPendingDeleteRequestsQuery, PagedResult<DeleteRequestDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetPendingDeleteRequestsQueryHandler> _logger;

    public GetPendingDeleteRequestsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetPendingDeleteRequestsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<DeleteRequestDto>> Handle(
        GetPendingDeleteRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting pending delete requests: Page={Page}, PageSize={PageSize}",
            query.PageNumber,
            query.PageSize);

        var dbQuery = _context.SharedGameDeleteRequests
            .AsNoTracking()
            .Where(r => r.Status == (int)DeleteRequestStatus.Pending)
            .Join(
                _context.SharedGames.AsNoTracking(),
                deleteReq => deleteReq.SharedGameId,
                game => game.Id,
                (deleteReq, game) => new DeleteRequestDto(
                    deleteReq.Id,
                    deleteReq.SharedGameId,
                    game.Title,
                    deleteReq.RequestedBy,
                    deleteReq.Reason,
                    deleteReq.CreatedAt))
            .OrderByDescending(d => d.CreatedAt);

        // Pagination
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var deleteRequests = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} pending delete requests (Total: {Total}) for page {Page}",
            deleteRequests.Count,
            total,
            query.PageNumber);

        return new PagedResult<DeleteRequestDto>(
            Items: deleteRequests,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }
}
