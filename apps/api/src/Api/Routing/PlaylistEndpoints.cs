using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.Playlists;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Game Night Playlist endpoints.
/// Issue #5582: Game Night Playlist backend CRUD with sharing.
/// </summary>
internal static class PlaylistEndpoints
{
    public static RouteGroupBuilder MapPlaylistEndpoints(this RouteGroupBuilder group)
    {
        var playlists = group.MapGroup("/playlists")
            .WithTags("Playlists");

        // Create playlist
        playlists.MapPost("/", async (
            [FromBody] CreatePlaylistRequest request,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new CreatePlaylistCommand(
                Name: request.Name,
                CreatorUserId: userId,
                ScheduledDate: request.ScheduledDate);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/playlists/{result.Id}", result);
        })
        .RequireAuthorization()
        .WithName("CreatePlaylist")
        .Produces<GameNightPlaylistDto>(201)
        .Produces(400);

        // List user's playlists (paginated)
        playlists.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var query = new GetUserPlaylistsQuery(userId, page, pageSize);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetUserPlaylists")
        .Produces<PaginatedPlaylistsResponse>();

        // Get playlist by ID
        playlists.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var query = new GetPlaylistQuery(id, userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetPlaylistById")
        .Produces<GameNightPlaylistDto>()
        .Produces(404);

        // Update playlist
        playlists.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePlaylistRequest request,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new UpdatePlaylistCommand(
                PlaylistId: id,
                UserId: userId,
                Name: request.Name,
                ScheduledDate: request.ScheduledDate);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("UpdatePlaylist")
        .Produces<GameNightPlaylistDto>()
        .Produces(404);

        // Delete playlist (soft delete)
        playlists.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new DeletePlaylistCommand(id, userId);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeletePlaylist")
        .Produces(204)
        .Produces(404);

        // Add game to playlist
        playlists.MapPost("/{id:guid}/games", async (
            Guid id,
            [FromBody] AddGameToPlaylistRequest request,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new AddGameToPlaylistCommand(
                PlaylistId: id,
                UserId: userId,
                SharedGameId: request.SharedGameId,
                Position: request.Position);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("AddGameToPlaylist")
        .Produces<GameNightPlaylistDto>()
        .Produces(404);

        // Remove game from playlist
        playlists.MapDelete("/{id:guid}/games/{gameId:guid}", async (
            Guid id,
            Guid gameId,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new RemoveGameFromPlaylistCommand(id, userId, gameId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("RemoveGameFromPlaylist")
        .Produces<GameNightPlaylistDto>()
        .Produces(404);

        // Reorder games in playlist
        playlists.MapPut("/{id:guid}/games/reorder", async (
            Guid id,
            [FromBody] ReorderPlaylistGamesRequest request,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new ReorderPlaylistGamesCommand(id, userId, request.OrderedGameIds);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ReorderPlaylistGames")
        .Produces<GameNightPlaylistDto>()
        .Produces(404);

        // Generate share link
        playlists.MapPost("/{id:guid}/share", async (
            Guid id,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new GenerateShareLinkCommand(id, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GeneratePlaylistShareLink")
        .Produces<ShareLinkResponse>()
        .Produces(404);

        // Revoke share link
        playlists.MapDelete("/{id:guid}/share", async (
            Guid id,
            [FromServices] IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            var command = new RevokeShareLinkCommand(id, userId);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("RevokePlaylistShareLink")
        .Produces(204)
        .Produces(404);

        // Get playlist by share token (PUBLIC endpoint)
        playlists.MapGet("/shared/{token}", async (
            string token,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetPlaylistByShareTokenQuery(token);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetPlaylistByShareToken")
        .Produces<GameNightPlaylistDto>()
        .Produces(404);

        return group;
    }
}
