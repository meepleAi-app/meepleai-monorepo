using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Wishlist management endpoints.
/// Issue #3917: Wishlist Management API (CRUD).
/// </summary>
internal static class WishlistEndpoints
{
    public static RouteGroupBuilder MapWishlistEndpoints(this RouteGroupBuilder group)
    {
        MapGetWishlistEndpoint(group);
        MapGetWishlistHighlightsEndpoint(group);
        MapAddToWishlistEndpoint(group);
        MapUpdateWishlistItemEndpoint(group);
        MapRemoveFromWishlistEndpoint(group);

        return group;
    }

    private static void MapGetWishlistEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/wishlist", async (
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

            var query = new GetWishlistQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<WishlistItemDto>>(200)
        .Produces(401)
        .WithTags("Wishlist")
        .WithSummary("Get user's wishlist")
        .WithDescription("Returns the complete wishlist for the authenticated user, ordered by priority (high first) then by date added.")
        .WithOpenApi();
    }

    private static void MapGetWishlistHighlightsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/wishlist/highlights", async (
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

            var query = new GetWishlistHighlightsQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<WishlistItemDto>>(200)
        .Produces(401)
        .WithTags("Wishlist")
        .WithSummary("Get wishlist highlights")
        .WithDescription("Returns the top 5 highest-priority wishlist items for the authenticated user.")
        .WithOpenApi();
    }

    private static void MapAddToWishlistEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/wishlist", async (
            [FromBody] AddToWishlistRequest request,
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

            var command = new AddToWishlistCommand(
                UserId: userId,
                GameId: request.GameId,
                Priority: request.Priority,
                TargetPrice: request.TargetPrice,
                Notes: request.Notes
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/wishlist/{result.Id}", result);
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<WishlistItemDto>(201)
        .Produces(401)
        .Produces(409)
        .WithTags("Wishlist")
        .WithSummary("Add game to wishlist")
        .WithDescription("Adds a game to the user's wishlist with priority, optional target price and notes. Returns 409 if game is already on wishlist.")
        .WithOpenApi();
    }

    private static void MapUpdateWishlistItemEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/wishlist/{id:guid}", async (
            Guid id,
            [FromBody] UpdateWishlistItemRequest request,
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

            var command = new UpdateWishlistItemCommand(
                UserId: userId,
                WishlistItemId: id,
                Priority: request.Priority,
                TargetPrice: request.TargetPrice,
                ClearTargetPrice: request.ClearTargetPrice,
                Notes: request.Notes,
                ClearNotes: request.ClearNotes
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden");
            }
        })
        .RequireAuthenticatedUser()
        .Produces<WishlistItemDto>(200)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Wishlist")
        .WithSummary("Update wishlist item")
        .WithDescription("Updates priority, target price, and/or notes for a wishlist item. Use clearTargetPrice/clearNotes to set those fields to null.")
        .WithOpenApi();
    }

    private static void MapRemoveFromWishlistEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/wishlist/{id:guid}", async (
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

            var command = new RemoveFromWishlistCommand(userId, id);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ForbiddenException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden");
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Wishlist")
        .WithSummary("Remove from wishlist")
        .WithDescription("Removes a game from the user's wishlist. Returns 404 if not found, 403 if not owned.")
        .WithOpenApi();
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
/// Request body for adding a game to wishlist.
/// </summary>
public record AddToWishlistRequest(
    Guid GameId,
    string Priority,
    decimal? TargetPrice = null,
    string? Notes = null
);

/// <summary>
/// Request body for updating a wishlist item.
/// </summary>
public record UpdateWishlistItemRequest(
    string? Priority = null,
    decimal? TargetPrice = null,
    bool ClearTargetPrice = false,
    string? Notes = null,
    bool ClearNotes = false
);
