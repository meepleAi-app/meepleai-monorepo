using Api.SharedKernel.Application.Interfaces;
using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed class GetDiscoverDataHandler : IQueryHandler<GetDiscoverDataQuery, DiscoverDto>
{
    private readonly ILogger<GetDiscoverDataHandler> _logger;

    public GetDiscoverDataHandler(ILogger<GetDiscoverDataHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DiscoverDto> Handle(GetDiscoverDataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Task.FromResult(new DiscoverDto(
            NewGames: Array.Empty<NewGameDto>(),
            TopAgents: Array.Empty<TopAgentDto>(),
            RecommendedToolkits: Array.Empty<RecommendedToolkitDto>(),
            RecentKb: Array.Empty<RecentKbDocDto>(),
            TopContributors: Array.Empty<TopContributorDto>()
        ));
    }
}
