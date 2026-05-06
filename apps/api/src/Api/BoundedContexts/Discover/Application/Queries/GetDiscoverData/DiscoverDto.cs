using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed record DiscoverDto(
    IReadOnlyList<NewGameDto> NewGames,
    IReadOnlyList<TopAgentDto> TopAgents,
    IReadOnlyList<RecommendedToolkitDto> RecommendedToolkits,
    IReadOnlyList<RecentKbDocDto> RecentKb,
    IReadOnlyList<TopContributorDto> TopContributors
);
