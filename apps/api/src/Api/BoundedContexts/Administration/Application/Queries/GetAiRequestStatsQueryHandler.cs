using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal class GetAiRequestStatsQueryHandler : IQueryHandler<GetAiRequestStatsQuery, AiRequestStats>
{
    private readonly MeepleAiDbContext _db;

    public GetAiRequestStatsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AiRequestStats> Handle(GetAiRequestStatsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = _db.AiRequestLogs
            .AsNoTracking() // PERF-05: Read-only analytics query
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= request.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId) && Guid.TryParse(request.UserId, out var userGuid))
        {
            query = query.Where(log => log.UserId == userGuid);
        }

        if (!string.IsNullOrWhiteSpace(request.GameId) && Guid.TryParse(request.GameId, out var gameGuid))
        {
            query = query.Where(log => log.GameId == gameGuid);
        }

        // PERF-03: Single aggregate query instead of 4 separate queries
        var stats = await query
            .GroupBy(_ => 1) // Group all into single aggregate
            .Select(g => new
            {
                TotalRequests = g.Count(),
                AvgLatencyMs = g.Average(log => (double?)log.LatencyMs) ?? 0,
                TotalTokens = g.Sum(log => log.TokenCount),
                SuccessCount = g.Count(log => log.Status == "Success")
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        var endpointCounts = await query
            .GroupBy(log => log.Endpoint)
            .Select(g => new { Endpoint = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (stats == null)
        {
            // No data found - return empty stats
            return new AiRequestStats
            {
                TotalRequests = 0,
                AvgLatencyMs = 0,
                TotalTokens = 0,
                SuccessRate = 0,
                EndpointCounts = new Dictionary<string, int>(StringComparer.Ordinal)
            };
        }

        return new AiRequestStats
        {
            TotalRequests = stats.TotalRequests,
            AvgLatencyMs = stats.AvgLatencyMs,
            TotalTokens = stats.TotalTokens,
            SuccessRate = stats.TotalRequests > 0 ? (double)stats.SuccessCount / stats.TotalRequests : 0,
            EndpointCounts = endpointCounts.ToDictionary(x => x.Endpoint, x => x.Count, StringComparer.Ordinal)
        };
    }
}
