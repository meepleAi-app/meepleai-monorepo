using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed class GetDiscoverDataHandler : IQueryHandler<GetDiscoverDataQuery, DiscoverDto>
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetDiscoverDataHandler> _logger;

    public GetDiscoverDataHandler(IMediator mediator, ILogger<GetDiscoverDataHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DiscoverDto> Handle(GetDiscoverDataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var newGamesTask = SafeRun("newGames", () => _mediator.Send(new GetNewGamesQuery(query.Limit), cancellationToken), Array.Empty<NewGameDto>());
        var topAgentsTask = SafeRun("topAgents", () => _mediator.Send(new GetTopAgentsQuery(query.Limit), cancellationToken), Array.Empty<TopAgentDto>());
        var toolkitsTask = SafeRun("recommendedToolkits", () => _mediator.Send(new GetRecommendedToolkitsQuery(query.Limit), cancellationToken), Array.Empty<RecommendedToolkitDto>());
        var recentKbTask = SafeRun("recentKb", () => _mediator.Send(new GetRecentKbDocumentsQuery(query.Limit), cancellationToken), Array.Empty<RecentKbDocDto>());
        var contributorsTask = SafeRun("topContributors", () => _mediator.Send(new GetTopContributorsQuery(query.Limit), cancellationToken), Array.Empty<TopContributorDto>());

        await Task.WhenAll(newGamesTask, topAgentsTask, toolkitsTask, recentKbTask, contributorsTask).ConfigureAwait(false);

        return new DiscoverDto(
            NewGames: await newGamesTask.ConfigureAwait(false),
            TopAgents: await topAgentsTask.ConfigureAwait(false),
            RecommendedToolkits: await toolkitsTask.ConfigureAwait(false),
            RecentKb: await recentKbTask.ConfigureAwait(false),
            TopContributors: await contributorsTask.ConfigureAwait(false)
        );
    }

    private async Task<IReadOnlyList<T>> SafeRun<T>(string rowName, Func<Task<IReadOnlyList<T>>> factory, IReadOnlyList<T> fallback)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discover row {RowName} failed", rowName);
            return fallback;
        }
    }
}
