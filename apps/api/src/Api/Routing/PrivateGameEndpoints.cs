using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.Extensions;
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
        MapAddPrivateGameEndpoint(group);
        MapGetPrivateGameEndpoint(group);
        MapUpdatePrivateGameEndpoint(group);
        MapDeletePrivateGameEndpoint(group);
        MapProposePrivateGameEndpoint(group);

        return group;
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
                ComplexityRating: request.ComplexityRating,
                ImageUrl: request.ImageUrl,
                ThumbnailUrl: request.ThumbnailUrl
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
                ComplexityRating: request.ComplexityRating,
                ImageUrl: request.ImageUrl
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
    /// POST /api/v1/private-games/{id}/propose-to-catalog - Propose a private game to shared catalog
    /// Rate limit: 2 requests per minute
    /// Issue #3665: Phase 4 - Proposal System
    /// </summary>
    private static void MapProposePrivateGameEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/private-games/{id:guid}/propose-to-catalog", async (
            Guid id,
            [FromBody] ProposePrivateGameRequest request,
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

            var command = new ProposePrivateGameCommand(
                UserId: userId,
                PrivateGameId: id,
                Notes: request.Notes,
                AttachedDocumentIds: request.AttachedDocumentIds
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Created($"/api/v1/share-requests/{result.ShareRequestId}", result);
        })
        .RequireAuthorization()
        .RequireRateLimiting("ProposePrivateGame")
        .WithName("ProposePrivateGameToCatalog")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Propose private game to catalog";
            operation.Description = "Submit a proposal to add your private game to the shared catalog. " +
                "The proposal will be reviewed by admins. Rate limited to 2 requests per minute.";
            return operation;
        })
        .ProducesValidationProblem()
        .Produces<Api.BoundedContexts.SharedGameCatalog.Application.Commands.CreateShareRequestResponse>(StatusCodes.Status201Created)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests);
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.User!.Id;
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
    decimal? ComplexityRating = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null
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
    decimal? ComplexityRating,
    string? ImageUrl
);

/// <summary>
/// Request DTO for proposing a private game to catalog.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
internal record ProposePrivateGameRequest(
    string? Notes = null,
    List<Guid>? AttachedDocumentIds = null
);
