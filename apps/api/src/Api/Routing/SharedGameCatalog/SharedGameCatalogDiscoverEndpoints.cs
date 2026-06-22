using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// /discover-surface SharedGameCatalog endpoints (Wave 3 Phase 1, PR #732 §4.3 / Issue #805).
/// Lives next to <see cref="SharedGameCatalogTrendingEndpoints"/> because both feed the
/// same SP4 /discover route but rank/sort by different signals.
/// </summary>
internal static class SharedGameCatalogDiscoverEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/catalog/games/new", HandleGetNewGames)
            .RequireAuthorization()
            .WithName("GetNewGames")
            .WithTags("Discover")
            .WithSummary("Get the most recently created games")
            .WithDescription(
                "Returns the most recently created shared games sorted by createdAt DESC. "
                + "Powers the SP4 /discover \"New games\" rail. "
                + "Soft-deleted rows excluded. Cache: 1h via HybridCache.")
            .Produces<DiscoverItemsEnvelope<NewGameDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleGetNewGames(
        [FromQuery] int? limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var safeLimit = limit.GetValueOrDefault(10);
        if (safeLimit < 1)
        {
            safeLimit = 10;
        }
        if (safeLimit > 50)
        {
            safeLimit = 50;
        }

        var query = new GetNewGamesQuery(safeLimit);
        var items = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Results.Ok(new DiscoverItemsEnvelope<NewGameDto>(items));
    }
}

/// <summary>
/// Stable response envelope for /discover endpoints — wraps the raw item list in
/// <c>{ items: [...] }</c> per PR #732 §3.4 empty-state contract (200 with empty
/// array rather than 404 / 204).
/// </summary>
/// <typeparam name="T">Item DTO type.</typeparam>
internal sealed record DiscoverItemsEnvelope<T>(IReadOnlyList<T> Items);
