// GameManagement API Routes
//
// Issue #4273: Added SearchGamesQuery endpoint
// Issue #4903: Added GetGameStrategiesQuery endpoint

using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Extensions;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.BoundedContexts.GameManagement.Routing;

public static class GameManagementRoutes
{
    public static RouteGroupBuilder MapGameManagementEndpoints(this RouteGroupBuilder group)
    {
        // Issue #4273: Game search autocomplete
        group.MapGet("/games/search", async (
            [FromQuery] string q,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();

            var results = await mediator.Send(
                new SearchGamesQuery(q, userId),
                ct)
                .ConfigureAwait(false);

            return Results.Ok(results);
        })
        .RequireAuthorization()
        .WithName("SearchGames")
        .WithTags("GameManagement")
        .Produces<List<GameSearchResultDto>>();

        // Issue #4903: Get paginated strategies for a game
        group.MapGet("/games/{gameId:guid}/strategies", async (
            Guid gameId,
            [FromServices] IMediator mediator,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new GetGameStrategiesQuery(gameId, pageNumber, pageSize),
                ct)
                .ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetGameStrategies")
        .WithSummary("Get strategies for a game")
        .WithDescription("Returns paginated strategy entries (tips, tactics, winning guides) for a game. Ordered by upvotes descending.")
        .WithTags("GameManagement")
        .Produces<PagedResult<GameStrategyDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }
}
