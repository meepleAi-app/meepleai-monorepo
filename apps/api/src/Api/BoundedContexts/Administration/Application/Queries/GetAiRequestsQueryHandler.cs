using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal class GetAiRequestsQueryHandler : IQueryHandler<GetAiRequestsQuery, AiRequestListResult>
{
    private readonly MeepleAiDbContext _db;

    public GetAiRequestsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AiRequestListResult> Handle(GetAiRequestsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = _db.AiRequestLogs
            .AsNoTracking() // PERF-05: Read-only analytics query
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Endpoint))
        {
            query = query.Where(log => log.Endpoint == request.Endpoint);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId) && Guid.TryParse(request.UserId, out var userGuid))
        {
            query = query.Where(log => log.UserId == userGuid);
        }

        if (!string.IsNullOrWhiteSpace(request.GameId) && Guid.TryParse(request.GameId, out var gameGuid))
        {
            query = query.Where(log => log.GameId == gameGuid);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= request.EndDate.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        // Get paginated results
        var requests = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AiRequestListResult
        {
            Requests = requests,
            TotalCount = totalCount
        };
    }
}
