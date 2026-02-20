// GameManagement API Routes
//
// Issue #4273: Added SearchGamesQuery endpoint
// Issue #4903: Added GetGameStrategiesQuery endpoint
// Issue #4904: Added GetGameReviewsQuery + CreateGameReviewCommand endpoints

using Api.BoundedContexts.GameManagement.Application.Commands.GameReviews;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries.GameReviews;
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

        // Issue #4904: Get paginated reviews for a game
        group.MapGet("/games/{gameId:guid}/reviews", async (
            Guid gameId,
            [FromServices] IMediator mediator,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new GetGameReviewsQuery(gameId, pageNumber, pageSize),
                ct)
                .ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetGameReviews")
        .WithSummary("Get reviews for a game")
        .WithDescription("Returns paginated user reviews for a game. Ordered by creation date descending.")
        .WithTags("GameManagement")
        .Produces<PagedResult<GameReviewDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        // Issue #4904: Submit a review for a game (one per user per game)
        group.MapPost("/games/{gameId:guid}/reviews", async (
            Guid gameId,
            [FromBody] CreateGameReviewRequest request,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();

            var dto = await mediator.Send(
                new CreateGameReviewCommand(gameId, userId, request.AuthorName, request.Rating, request.Content),
                ct)
                .ConfigureAwait(false);

            return Results.Created($"/api/v1/games/{gameId}/reviews/{dto.Id}", dto);
        })
        .RequireAuthorization()
        .WithName("CreateGameReview")
        .WithSummary("Submit a review for a game")
        .WithDescription("Allows an authenticated user to submit a review for a game. Each user may submit at most one review per game.")
        .WithTags("GameManagement")
        .Produces<GameReviewDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict);

        return group;
    }
}

/// <summary>
/// Request body for creating a game review.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
public record CreateGameReviewRequest(string AuthorName, int Rating, string Content);
