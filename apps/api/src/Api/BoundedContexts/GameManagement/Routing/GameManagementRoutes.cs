// GameManagement API Routes
//
// Issue #4273: Added SearchGamesQuery endpoint

using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.Extensions;
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

        return group;
    }
}
