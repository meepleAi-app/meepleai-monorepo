using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
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
        MapProposePrivateGameEndpoint(group);
        MapLinkAgentEndpoint(group); // Issue #4228
        MapUnlinkAgentEndpoint(group); // Issue #4228

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

    /// <summary>
    /// POST /api/v1/private-games/{id}/link-agent/{agentId} - Link AI agent to private game
    /// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
    /// </summary>
    private static void MapLinkAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/private-games/{id:guid}/link-agent/{agentId:guid}", async (
            Guid id,
            Guid agentId,
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

            var command = new LinkAgentToPrivateGameCommand(id, agentId, userId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                // Agent already linked
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                // Not the owner
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization()
        .WithName("LinkAgentToPrivateGame")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Link AI agent to private game";
            operation.Description = "Links an AI agent definition to your private game for personalized assistance.";
            return operation;
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// DELETE /api/v1/private-games/{id}/unlink-agent - Unlink AI agent from private game
    /// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
    /// </summary>
    private static void MapUnlinkAgentEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/private-games/{id:guid}/unlink-agent", async (
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

            var command = new UnlinkAgentFromPrivateGameCommand(id, userId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                // No agent linked
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                // Not the owner
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireAuthorization()
        .WithName("UnlinkAgentFromPrivateGame")
        .WithTags("PrivateGames")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Unlink AI agent from private game";
            operation.Description = "Removes the AI agent link from your private game.";
            return operation;
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
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
