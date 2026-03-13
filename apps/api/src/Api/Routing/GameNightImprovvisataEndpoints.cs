using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Endpoints for the Game Night Improvvisata feature.
/// Handles BGG search, BGG import and session-level AI orchestration flows.
/// Game Night Improvvisata - E1-1: User-facing BGG search via CQRS.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
internal static class GameNightImprovvisataEndpoints
{
    public static RouteGroupBuilder MapGameNightImprovvisataEndpoints(this RouteGroupBuilder group)
    {
        var improvvisata = group.MapGroup("/game-night")
            .WithTags("GameNightImprovvisata");

        // E1-1: BGG search for authenticated users
        improvvisata.MapGet("/bgg/search", HandleSearchBggGames)
            .RequireAuthorization()
            .RequireRateLimiting("BggSearch")
            .Produces<SearchBggGamesForGameNightResult>(200)
            .Produces(400)
            .Produces(401)
            .WithName("GameNightSearchBggGames")
            .WithSummary("Search BoardGameGeek for games")
            .WithDescription("Authenticated search for board games on BoardGameGeek. Used in the Game Night Improvvisata flow for game selection. Rate limited to 20 searches per hour per user.");

        // E1-2: Import BGG game into user's private library
        improvvisata.MapPost("/import-bgg", HandleImportBggGame)
            .RequireAuthorization()
            .Produces<ImportBggGameResult>(201)
            .Produces(400)
            .Produces(401)
            .Produces(409)
            .Produces(429)
            .WithName("ImportBggGame")
            .WithSummary("Import a BGG game as a PrivateGame")
            .WithDescription("Imports a game from BoardGameGeek into the user's private library, subject to tier quotas.");

        return group;
    }

    #region Query Handlers

    private static async Task<IResult> HandleSearchBggGames(
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "page")] int page,
        [FromQuery(Name = "pageSize")] int pageSize,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new SearchBggGamesForGameNightQuery(q ?? string.Empty, page, pageSize);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    #endregion

    #region Command Handlers

    private static async Task<IResult> HandleImportBggGame(
        [FromBody] ImportBggGameRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();

        var command = new ImportBggGameCommand(
            UserId: userId,
            BggId: request.BggId);

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/game-night/private-games/{result.PrivateGameId}", result);
    }

    #endregion

    #region Request Records

    private sealed record ImportBggGameRequest(int BggId);

    #endregion
}
