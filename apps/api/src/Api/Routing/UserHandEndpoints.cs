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
/// Hand slot management endpoints — the quick-access "La Mia Mano" sidebar.
/// Each user has 4 fixed slots (toolkit, game, session, ai).
/// </summary>
internal static class UserHandEndpoints
{
    public static RouteGroupBuilder MapUserHandEndpoints(this RouteGroupBuilder group)
    {
        MapGetUserHandEndpoint(group);
        MapUpdateHandSlotEndpoint(group);
        MapClearHandSlotEndpoint(group);

        return group;
    }

    private static void MapGetUserHandEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/hand", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var query = new GetUserHandQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<UserHandSlotDto>>(200)
        .Produces(401)
        .WithTags("MyHand")
        .WithSummary("Get user hand slots")
        .WithDescription("Returns all 4 hand slots for the authenticated user. Empty slots have null entity fields.")
        .WithOpenApi();
    }

    private static void MapUpdateHandSlotEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/users/me/hand/{slotType}", async (
            string slotType,
            [FromBody] UpdateHandSlotRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var command = new UpdateHandSlotCommand(
                UserId: userId,
                SlotType: slotType,
                EntityId: request.EntityId,
                EntityType: request.EntityType,
                EntityLabel: request.EntityLabel,
                EntityImageUrl: request.EntityImageUrl
            );
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<UserHandSlotDto>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("MyHand")
        .WithSummary("Assign entity to hand slot")
        .WithDescription("Assigns an entity (game, toolkit, session, or agent) to the specified hand slot. Valid slot types: toolkit, game, session, ai.")
        .WithOpenApi();
    }

    private static void MapClearHandSlotEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/users/me/hand/{slotType}", async (
            string slotType,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
                return Results.Unauthorized();

            var command = new ClearHandSlotCommand(UserId: userId, SlotType: slotType);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .WithTags("MyHand")
        .WithSummary("Clear hand slot")
        .WithDescription("Removes the entity assignment from the specified hand slot. Valid slot types: toolkit, game, session, ai.")
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
            return true;

        return false;
    }
}

/// <summary>
/// Request body for assigning an entity to a hand slot.
/// </summary>
public record UpdateHandSlotRequest(
    Guid EntityId,
    string EntityType,
    string? EntityLabel = null,
    string? EntityImageUrl = null
);
