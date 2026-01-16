using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User library endpoints.
/// Handles user game library management: adding, removing, updating, and viewing games.
/// </summary>
internal static class UserLibraryEndpoints
{
    public static RouteGroupBuilder MapUserLibraryEndpoints(this RouteGroupBuilder group)
    {
        MapGetUserLibraryEndpoint(group);
        MapGetLibraryStatsEndpoint(group);
        MapGetLibraryQuotaEndpoint(group);
        MapAddGameToLibraryEndpoint(group);
        MapRemoveGameFromLibraryEndpoint(group);
        MapUpdateLibraryEntryEndpoint(group);
        MapGetGameInLibraryStatusEndpoint(group);

        return group;
    }

    private static void MapGetUserLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] bool? favoritesOnly,
            [FromQuery] string? sortBy,
            [FromQuery] bool? sortDescending,
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

            var query = new GetUserLibraryQuery(
                UserId: userId,
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                FavoritesOnly: favoritesOnly,
                SortBy: sortBy ?? "addedAt",
                Descending: sortDescending ?? true
            );
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<PaginatedLibraryResponseDto>(200)
        .WithTags("Library")
        .WithSummary("Get user's game library")
        .WithDescription("Returns paginated list of games in user's library. Supports filtering by favorites and sorting by addedAt, title, or favorite status.");
    }

    private static void MapGetLibraryStatsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/stats", async (
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

            var query = new GetLibraryStatsQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryStatsDto>(200)
        .WithTags("Library")
        .WithSummary("Get library statistics")
        .WithDescription("Returns statistics about user's library including total games, favorites count, and date range.");
    }

    private static void MapGetLibraryQuotaEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/quota", async (
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

            var query = new GetLibraryQuotaQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<LibraryQuotaDto>(200)
        .WithTags("Library")
        .WithSummary("Get library quota")
        .WithDescription("Returns quota information for user's library including games in library, max allowed, remaining slots, and tier.");
    }

    private static void MapAddGameToLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}", async (
            Guid gameId,
            [FromBody] AddGameToLibraryRequest? request,
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

            var command = new AddGameToLibraryCommand(
                UserId: userId,
                GameId: gameId,
                Notes: request?.Notes,
                IsFavorite: request?.IsFavorite ?? false
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/games/{gameId}", result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in library"))
            {
                return Results.Conflict(new { error = "Game is already in library" });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryEntryDto>(201)
        .Produces(401)
        .Produces(409)
        .WithTags("Library")
        .WithSummary("Add game to library")
        .WithDescription("Adds a game to user's library with optional notes and favorite status. Returns 409 if game already in library.");
    }

    private static void MapRemoveGameFromLibraryEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/games/{gameId:guid}", async (
            Guid gameId,
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

            var command = new RemoveGameFromLibraryCommand(userId, gameId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "Game not found in library" });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .WithTags("Library")
        .WithSummary("Remove game from library")
        .WithDescription("Removes a game from user's library. Returns 404 if game not in library.");
    }

    private static void MapUpdateLibraryEntryEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/library/games/{gameId:guid}", async (
            Guid gameId,
            [FromBody] UpdateLibraryEntryRequest request,
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

            var command = new UpdateLibraryEntryCommand(
                UserId: userId,
                GameId: gameId,
                Notes: request.Notes,
                IsFavorite: request.IsFavorite
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "Game not found in library" });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<UserLibraryEntryDto>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Library")
        .WithSummary("Update library entry")
        .WithDescription("Updates notes and/or favorite status for a game in user's library. Returns 404 if game not in library.");
    }

    private static void MapGetGameInLibraryStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/status", async (
            Guid gameId,
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

            var query = new GetGameInLibraryStatusQuery(userId, gameId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<GameInLibraryStatusDto>(200)
        .Produces(401)
        .WithTags("Library")
        .WithSummary("Check if game is in library")
        .WithDescription("Returns whether a game is in user's library and if it's marked as favorite.");
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
/// Request body for adding a game to library.
/// </summary>
public record AddGameToLibraryRequest(
    string? Notes = null,
    bool IsFavorite = false
);

/// <summary>
/// Request body for updating a library entry.
/// </summary>
public record UpdateLibraryEntryRequest(
    string? Notes = null,
    bool? IsFavorite = null
);
