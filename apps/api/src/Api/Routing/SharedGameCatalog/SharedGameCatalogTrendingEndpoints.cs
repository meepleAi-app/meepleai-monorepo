using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Trending analytics endpoints for SharedGameCatalog (Issue #3918).
/// </summary>
internal static class SharedGameCatalogTrendingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Get trending games
        group.MapGet("/catalog/trending", HandleGetCatalogTrending)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetCatalogTrending")
            .WithSummary("Get top trending games in the catalog")
            .WithDescription("Returns the top trending games based on weighted analytics events (searches, views, library additions, plays) from the last 7 days with time-decay scoring.")
            .Produces<List<TrendingGameDto>>()
            .Produces(StatusCodes.Status400BadRequest);

        // Record a game analytics event
        group.MapPost("/catalog/events", HandleRecordGameEvent)
            .RequireAuthorization()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("RecordGameEvent")
            .WithSummary("Record a game analytics event")
            .WithDescription("Records a game interaction event (search, view, library addition, play) for trending analytics.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> HandleGetCatalogTrending(
        [FromQuery] int limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetCatalogTrendingQuery(limit > 0 ? limit : 10);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRecordGameEvent(
        RecordGameEventRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RecordGameEventCommand(
            request.GameId,
            request.EventType,
            request.UserId);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
