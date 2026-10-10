using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Private game endpoints for user-owned games not in the shared catalog.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
internal static class PrivateGameEndpoints
{
    public static RouteGroupBuilder MapPrivateGameEndpoints(this RouteGroupBuilder group)
    {
        MapGetPrivateGamesListEndpoint(group);
        MapAddPrivateGameEndpoint(group);
        MapGetPrivateGameEndpoint(group);
        MapUpdatePrivateGameEndpoint(group);
        MapDeletePrivateGameEndpoint(group);
        // Issue #4138: link-agent / unlink-agent (Issue #4228) are retired — one system agent
        // for every game (ADR-094). EndpointContractTests.RetiredRoutes keeps them off.
        MapKnowledgeBaseStatusEndpoint(group); // Issue #3664

        return group;
    }

    /// <summary>
    /// GET /api/v1/private-games - List private games with pagination, search, and sorting
    /// </summary>
    private static void MapGetPrivateGamesListEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/private-games", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetPrivateGamesListQuery(
                UserId: userId,
                Page: page ?? 1,
                PageSize: Math.Min(pageSize ?? 12, 50),
                Search: search,
                SortBy: sortBy ?? "createdAt",
                SortDirection: sortDirection ?? "desc"
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetPrivateGamesList")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "List private games";
            operation.Description = "Returns a paginated list of the user's private games with optional search and sorting.";
            return operation;
        })
        .Produces<PaginatedPrivateGamesResponseDto>()
        .Produces(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// POST /api/v1/private-games - Add a private game
    /// Rate limit: 5 requests per minute
    /// </summary>
    private static void MapAddPrivateGameEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/private-games", async (
            [FromBody] AddPrivateGameRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new AddPrivateGameCommand(
                UserId: userId,
                Source: request.Source,
                BggId: request.BggId,
                Title: request.Title,
                MinPlayers: request.MinPlayers,
                MaxPlayers: request.MaxPlayers,
                YearPublished: request.YearPublished,
                Description: request.Description,
                PlayingTimeMinutes: request.PlayingTimeMinutes,
                MinAge: request.MinAge,
                ComplexityRating: request.ComplexityRating
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Created($"/api/v1/private-games/{result.Id}", result);
        })
        .RequireAuthorization()
        .WithName("AddPrivateGame")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Add a private game";
            operation.Description = "Add a game to your private library. Supports both manual entry and BoardGameGeek import. " +
                "Auto-redirects to shared catalog if BGG ID already exists there.";
            return operation;
        })
        .ProducesValidationProblem()
        .Produces<Api.BoundedContexts.UserLibrary.Application.DTOs.PrivateGameDto>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);
    }

    /// <summary>
    /// GET /api/v1/private-games/{id} - Get a private game
    /// </summary>
    private static void MapGetPrivateGameEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/private-games/{id:guid}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var query = new GetPrivateGameQuery(
                PrivateGameId: id,
                UserId: userId
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetPrivateGame")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get a private game";
            operation.Description = "Retrieve a private game by ID. You can only access your own private games.";
            return operation;
        })
        .Produces<Api.BoundedContexts.UserLibrary.Application.DTOs.PrivateGameDto>()
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// PUT /api/v1/private-games/{id} - Update a private game
    /// </summary>
    private static void MapUpdatePrivateGameEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/private-games/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePrivateGameRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new UpdatePrivateGameCommand(
                PrivateGameId: id,
                UserId: userId,
                Title: request.Title,
                MinPlayers: request.MinPlayers,
                MaxPlayers: request.MaxPlayers,
                YearPublished: request.YearPublished,
                Description: request.Description,
                PlayingTimeMinutes: request.PlayingTimeMinutes,
                MinAge: request.MinAge,
                ComplexityRating: request.ComplexityRating
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("UpdatePrivateGame")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Update a private game";
            operation.Description = "Update a private game's information. You can only update your own private games.";
            return operation;
        })
        .ProducesValidationProblem()
        .Produces<Api.BoundedContexts.UserLibrary.Application.DTOs.PrivateGameDto>()
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// DELETE /api/v1/private-games/{id} - Soft-delete a private game
    /// </summary>
    private static void MapDeletePrivateGameEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/private-games/{id:guid}", async (
            Guid id,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new DeletePrivateGameCommand(
                PrivateGameId: id,
                UserId: userId
            );

            await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeletePrivateGame")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Delete a private game";
            operation.Description = "Soft-delete a private game. You can only delete your own private games.";
            return operation;
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// GET /api/v1/private-games/{id}/kb-status - Get RAG knowledge base status for a private game
    /// Issue #3664: Private game PDF support — KB readiness polling.
    /// </summary>
    private static void MapKnowledgeBaseStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/private-games/{id:guid}/kb-status", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            // Issue #4137: the user id used to be discarded here (`out _`), so this
            // endpoint authenticated and then authorized nothing: any logged-in user
            // could read the KB status of any private game by id. It is now threaded
            // into the query, which the handler authorizes.
            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var query = new GetKnowledgeBaseStatusQuery(
                id,
                userId,
                session?.Principal?.EffectiveActor.Role ?? "User",
                IsPrivateGame: true);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetPrivateGameKbStatus")
        .WithTags("PrivateGames")
        .WithSummary("Get RAG knowledge base status for a private game");
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.Principal!.Subject.Id;
            return true;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Request DTO for adding a private game.
/// </summary>
internal record AddPrivateGameRequest(
    string Source,
    int? BggId,
    string Title,
    int MinPlayers,
    int MaxPlayers,
    int? YearPublished = null,
    string? Description = null,
    int? PlayingTimeMinutes = null,
    int? MinAge = null,
    decimal? ComplexityRating = null
);

/// <summary>
/// Request DTO for updating a private game.
/// </summary>
internal record UpdatePrivateGameRequest(
    string Title,
    int MinPlayers,
    int MaxPlayers,
    int? YearPublished,
    string? Description,
    int? PlayingTimeMinutes,
    int? MinAge,
    decimal? ComplexityRating
);
