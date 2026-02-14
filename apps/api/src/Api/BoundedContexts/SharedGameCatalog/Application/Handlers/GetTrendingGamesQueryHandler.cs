using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Handler for GetTrendingGamesQuery.
/// Issue #4310: Returns mock trending data (full implementation with GameTrendingScores table in follow-up)
/// </summary>
internal class GetTrendingGamesQueryHandler : IRequestHandler<GetTrendingGamesQuery, List<TrendingGameDto>>
{
    public async Task<List<TrendingGameDto>> Handle(GetTrendingGamesQuery request, CancellationToken cancellationToken)
    {
        // Mock data - placeholder until GameTrendingScores table and background job are implemented
        var mockTrending = new List<TrendingGameDto>
        {
            new(Guid.NewGuid(), "Wingspan", 95.5m, 15.0m, null),
            new(Guid.NewGuid(), "Terraforming Mars", 88.3m, 8.5m, null),
            new(Guid.NewGuid(), "Scythe", 82.1m, -2.3m, null),
            new(Guid.NewGuid(), "Gloomhaven", 79.4m, 12.1m, null),
            new(Guid.NewGuid(), "7 Wonders", 75.2m, 0.5m, null),
        };

        await Task.CompletedTask.ConfigureAwait(false);

        return mockTrending.Take(request.Limit).ToList();
    }
}
