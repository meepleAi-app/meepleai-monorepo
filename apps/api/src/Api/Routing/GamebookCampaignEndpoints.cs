using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Gamebook campaign endpoints for Libro Game (Iter 1.A).
/// Provides persistent save-state management for text-adventure gamebook sessions.
/// </summary>
internal static class GamebookCampaignEndpoints
{
    public static RouteGroupBuilder MapGamebookCampaignEndpoints(this RouteGroupBuilder group)
    {
        MapCreateCampaignEndpoint(group);
        MapListCampaignsEndpoint(group);
        MapGetCampaignEndpoint(group);
        MapUpdateProgressEndpoint(group);
        MapRenameCampaignEndpoint(group);
        MapDeleteCampaignEndpoint(group);

        return group;
    }

    private static void MapCreateCampaignEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/gamebook/campaigns", async (
            [FromBody] CreateGamebookCampaignRequest body,
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

            var dto = await mediator.Send(
                new CreateGamebookCampaignCommand(body.GameId, userId, body.Title), ct
            ).ConfigureAwait(false);

            return Results.Created($"/api/v1/gamebook/campaigns/{dto.Id}", dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GamebookCampaignDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithTags("Gamebook")
        .WithSummary("Create a new gamebook campaign session")
        .WithDescription("Creates a new persistent gamebook campaign for the authenticated user with the given game and title.")
        .WithOpenApi();
    }

    private static void MapListCampaignsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/campaigns", async (
            [FromQuery] Guid? gameId,
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

            var result = await mediator.Send(
                new ListMyGamebookCampaignsQuery(userId, gameId), ct
            ).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<GamebookCampaignDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithTags("Gamebook")
        .WithSummary("List gamebook campaigns for the current user")
        .WithDescription("Returns all gamebook campaigns belonging to the authenticated user. Optionally filter by gameId.")
        .WithOpenApi();
    }

    private static void MapGetCampaignEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/campaigns/{id:guid}", async (
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

            var dto = await mediator.Send(
                new GetGamebookCampaignQuery(id, userId), ct
            ).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GamebookCampaignDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Get a gamebook campaign by ID")
        .WithDescription("Returns the gamebook campaign with the specified ID, if it belongs to the authenticated user.")
        .WithOpenApi();
    }

    private static void MapUpdateProgressEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/gamebook/campaigns/{id:guid}/progress", async (
            Guid id,
            [FromBody] UpdateGamebookProgressRequest body,
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

            var dto = await mediator.Send(
                new UpdateGamebookProgressCommand(id, userId, body.GameBookId, body.CurrentParagraph), ct
            ).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GamebookCampaignDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Update the current paragraph progress for a gamebook campaign")
        .WithDescription("Advances (or navigates) the authenticated user's gamebook campaign to the specified paragraph, appending the previous position to the history stack.")
        .WithOpenApi();
    }

    private static void MapRenameCampaignEndpoint(RouteGroupBuilder group)
    {
        group.MapPatch("/gamebook/campaigns/{id:guid}", async (
            Guid id,
            [FromBody] RenameGamebookCampaignRequest body,
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

            var dto = await mediator.Send(
                new RenameGamebookCampaignCommand(id, userId, body.Title), ct
            ).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GamebookCampaignDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .WithTags("Gamebook")
        .WithSummary("Rename a gamebook campaign")
        .WithDescription("Updates the title of the campaign. Only the owner may rename.")
        .WithOpenApi();
    }

    private static void MapDeleteCampaignEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/gamebook/campaigns/{id:guid}", async (
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

            await mediator.Send(new DeleteGamebookCampaignCommand(id, userId), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .WithTags("Gamebook")
        .WithSummary("Soft-delete a gamebook campaign")
        .WithDescription("Marks the campaign as deleted (IsDeleted=true). Only the owner may delete. Photos and glossary entries are retained for audit but become unreachable.")
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

/// <summary>Request body for creating a new gamebook campaign.</summary>
public sealed record CreateGamebookCampaignRequest(Guid GameId, string Title);

/// <summary>
/// Request body for updating the current paragraph progress for a specific book.
/// C2 (2026-05-19): <paramref name="GameBookId"/> added to scope progress per-book
/// in support of multi-book campaigns (see <c>SessionBookProgress</c>).
/// </summary>
public sealed record UpdateGamebookProgressRequest(Guid GameBookId, int CurrentParagraph);

/// <summary>Request body for renaming a gamebook campaign.</summary>
public sealed record RenameGamebookCampaignRequest(string Title);
